using System.Collections.Generic;
using UnityEngine;

// Scene-local pools: inactive objects and active projectiles are cleaned up with the level.
public class CombatPool : MonoBehaviour
{
    private const int MaxCachedBullets = 128;
    private const int MaxCachedEffects = 64;
    private static CombatPool instance;

    private readonly Dictionary<GameObject, Stack<Bullet>> bullets = new Dictionary<GameObject, Stack<Bullet>>();
    private readonly Dictionary<GameObject, Stack<Effect>> effects = new Dictionary<GameObject, Stack<Effect>>();
    private readonly Stack<Effect> sounds = new Stack<Effect>();
    private readonly List<Effect> activeEffects = new List<Effect>(128);

    private sealed class Effect
    {
        public GameObject gameObject;
        public ParticleSystem[] particles;
        public AudioSource[] audio;
        public TrailRenderer[] trails;
        public Stack<Effect> pool;
        public float expiresAt;
    }

    private static CombatPool Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("Combat Pool").AddComponent<CombatPool>();
            return instance;
        }
    }

    public static void WarmBullets(GameObject prefab, int count)
    {
        if (prefab == null || prefab.GetComponent<Bullet>() == null)
            return;

        CombatPool host = Instance;
        Stack<Bullet> pool = host.GetBulletPool(prefab);
        // Each weapon reserves its own share before the first shot, including in Coop.
        int target = Mathf.Min(pool.Count + count, MaxCachedBullets);
        while (pool.Count < target)
            pool.Push(host.CreateBullet(prefab));

        GameObject spark = prefab.GetComponent<Bullet>().sparkPrefab;
        WarmEffect(spark, 40);
    }

    public static Bullet SpawnBullet(GameObject prefab, Vector3 position, Quaternion rotation,
        Collider[] ownerColliders, float range, int playerIndex)
    {
        if (prefab == null || prefab.GetComponent<Bullet>() == null)
            return null;

        CombatPool host = Instance;
        Stack<Bullet> pool = host.GetBulletPool(prefab);
        Bullet bullet = null;
        while (pool.Count > 0 && bullet == null)
            bullet = pool.Pop();
        if (bullet == null)
            bullet = host.CreateBullet(prefab);

        bullet.transform.SetPositionAndRotation(position, rotation);
        bullet.gameObject.SetActive(true);
        bullet.Launch(rotation * Vector3.forward, ownerColliders, range, playerIndex);
        return bullet;
    }

    private Stack<Bullet> GetBulletPool(GameObject prefab)
    {
        if (!bullets.TryGetValue(prefab, out Stack<Bullet> pool))
        {
            pool = new Stack<Bullet>(MaxCachedBullets);
            bullets.Add(prefab, pool);
        }
        return pool;
    }

    private Bullet CreateBullet(GameObject prefab)
    {
        Bullet bullet = Instantiate(prefab, transform).GetComponent<Bullet>();
        bullet.SetPool(this, prefab);
        bullet.gameObject.SetActive(false);
        return bullet;
    }

    internal void ReturnBullet(Bullet bullet, GameObject prefab)
    {
        bullet.gameObject.SetActive(false);
        Stack<Bullet> pool = GetBulletPool(prefab);
        if (pool.Count < MaxCachedBullets)
            pool.Push(bullet);
        else
            Destroy(bullet.gameObject);
    }

    public static void WarmEffect(GameObject prefab, int count)
    {
        if (prefab == null)
            return;

        CombatPool host = Instance;
        Stack<Effect> pool = host.GetEffectPool(prefab);
        int target = Mathf.Min(count, MaxCachedEffects);
        while (pool.Count < target)
            pool.Push(host.CreateEffect(prefab, pool));
    }

    public static void SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
    {
        if (prefab == null)
            return;

        CombatPool host = Instance;
        Stack<Effect> pool = host.GetEffectPool(prefab);
        Effect effect = host.TakeEffect(pool, prefab);
        host.ActivateEffect(effect, position, rotation, lifetime);
        foreach (ParticleSystem particle in effect.particles)
        {
            if (particle != null && particle.gameObject.activeInHierarchy)
                particle.Play(false);
        }
        foreach (AudioSource source in effect.audio)
        {
            if (source != null && source.clip != null && source.gameObject.activeInHierarchy)
                source.Play();
        }
    }

    public static void WarmSounds(int count)
    {
        CombatPool host = Instance;
        while (host.sounds.Count < Mathf.Min(count, MaxCachedEffects))
            host.sounds.Push(host.CreateEffect(null, host.sounds));
    }

    public static void PlayKillSound(AudioClip clip, float volume, Vector3 position)
    {
        if (clip == null)
            return;

        CombatPool host = Instance;
        Effect effect = host.TakeEffect(host.sounds, null);
        AudioSource source = effect.audio[0];
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 0f;
        host.ActivateEffect(effect, position, Quaternion.identity, clip.length + 0.1f);
        source.Play();
    }

    private Stack<Effect> GetEffectPool(GameObject prefab)
    {
        if (!effects.TryGetValue(prefab, out Stack<Effect> pool))
        {
            pool = new Stack<Effect>(MaxCachedEffects);
            effects.Add(prefab, pool);
        }
        return pool;
    }

    private Effect TakeEffect(Stack<Effect> pool, GameObject prefab)
    {
        while (pool.Count > 0)
        {
            Effect effect = pool.Pop();
            if (effect.gameObject != null)
                return effect;
        }
        return CreateEffect(prefab, pool);
    }

    private Effect CreateEffect(GameObject prefab, Stack<Effect> pool)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, transform);
        }
        else
        {
            go = new GameObject("ZombieKillSound", typeof(AudioSource));
            go.transform.SetParent(transform, false);
        }

        Effect effect = new Effect
        {
            gameObject = go,
            particles = go.GetComponentsInChildren<ParticleSystem>(true),
            audio = go.GetComponentsInChildren<AudioSource>(true),
            trails = go.GetComponentsInChildren<TrailRenderer>(true),
            pool = pool
        };

        foreach (ParticleSystem particle in effect.particles)
        {
            var main = particle.main;
            // The pool owns their lifetime, including prefabs that originally destroy themselves.
            main.stopAction = ParticleSystemStopAction.None;
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            main.playOnAwake = false;
        }
        foreach (AudioSource source in effect.audio)
        {
            source.Stop();
            source.playOnAwake = false;
        }
        go.SetActive(false);
        return effect;
    }

    private void ActivateEffect(Effect effect, Vector3 position, Quaternion rotation, float lifetime)
    {
        effect.gameObject.transform.SetPositionAndRotation(position, rotation);
        foreach (TrailRenderer trail in effect.trails)
            trail.Clear();
        effect.expiresAt = Time.time + lifetime;
        effect.gameObject.SetActive(true);
        activeEffects.Add(effect);
    }

    private void Update()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            Effect effect = activeEffects[i];
            if (effect.gameObject != null && Time.time < effect.expiresAt)
                continue;

            int last = activeEffects.Count - 1;
            activeEffects[i] = activeEffects[last];
            activeEffects.RemoveAt(last);
            if (effect.gameObject == null)
                continue;

            foreach (ParticleSystem particle in effect.particles)
            {
                if (particle != null)
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            foreach (AudioSource source in effect.audio)
            {
                if (source != null)
                    source.Stop();
            }
            effect.gameObject.SetActive(false);
            if (effect.pool.Count < MaxCachedEffects)
                effect.pool.Push(effect);
            else
                Destroy(effect.gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }
}
