using UnityEngine;
using UnityEngine.InputSystem;

public class CarWeapon : MonoBehaviour
{
    [Header("Weapon Setup")]
    public Transform[] gunPoints;
    public GameObject bulletPrefab;
    public float bulletRange = 200f;

    [Header("Audio")]
    public AudioSource gunAudioSource;
    public float gunFadeOutSpeed = 12f;

    [Header("Muzzle Flash")]
    public ParticleSystem[] muzzleFlashes;

    [Header("Shooting")]
    public float fireRate = 0.12f;

    private float nextFireTime;
    private Collider[] ownerColliders;
    private float originalGunVolume;

    private void Awake()
    {
        ownerColliders = GetComponentsInChildren<Collider>();

        if (gunAudioSource != null)
        {
            originalGunVolume = gunAudioSource.volume;
            gunAudioSource.playOnAwake = false;
            gunAudioSource.loop = true;
        }

        if (muzzleFlashes != null)
        {
            foreach (ParticleSystem flash in muzzleFlashes)
            {
                if (flash != null)
                {
                    flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }

    private void Update()
    {
        bool altPressed =
            Keyboard.current != null &&
            Keyboard.current.leftAltKey.isPressed;

        bool isFiring =
            !altPressed &&
            (
                (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
                (Gamepad.current != null && Gamepad.current.rightShoulder.isPressed)
            );

        if (isFiring)
        {
            Shoot();
            HandleGunAudioStart();
        }
        else
        {
            HandleGunAudioStop();
        }
    }

    private void Shoot()
    {
        if (Time.time < nextFireTime)
            return;

        if (bulletPrefab == null || gunPoints == null || gunPoints.Length == 0)
            return;

        nextFireTime = Time.time + fireRate;

        foreach (Transform gunPoint in gunPoints)
        {
            if (gunPoint == null)
                continue;

            Vector3 shootDirection = gunPoint.forward;

            GameObject bulletObject = Instantiate(
                bulletPrefab,
                gunPoint.position,
                gunPoint.rotation
            );

            Bullet bullet = bulletObject.GetComponent<Bullet>();

            if (bullet != null)
            {
                bullet.Launch(shootDirection, ownerColliders, bulletRange);
            }
        }

        PlayMuzzleFlash();
    }

    private void PlayMuzzleFlash()
    {
        if (muzzleFlashes == null)
            return;

        foreach (ParticleSystem flash in muzzleFlashes)
        {
            if (flash == null)
                continue;

            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            flash.Play();
        }
    }

    private void HandleGunAudioStart()
    {
        if (gunAudioSource == null)
            return;

        gunAudioSource.volume = originalGunVolume;

        if (!gunAudioSource.isPlaying)
        {
            gunAudioSource.Play();
        }
    }

    private void HandleGunAudioStop()
    {
        if (gunAudioSource == null)
            return;

        if (!gunAudioSource.isPlaying)
            return;

        gunAudioSource.volume = Mathf.MoveTowards(
            gunAudioSource.volume,
            0f,
            gunFadeOutSpeed * Time.deltaTime
        );

        if (gunAudioSource.volume <= 0.01f)
        {
            gunAudioSource.Stop();
            gunAudioSource.volume = originalGunVolume;
        }
    }
}