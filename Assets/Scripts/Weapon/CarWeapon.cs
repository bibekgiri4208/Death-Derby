using UnityEngine;
using UnityEngine.InputSystem;

public class CarWeapon : MonoBehaviour
{
    [Header("Weapon Setup")]
    public Transform gunPivot;
    public Transform gunPoint;
    public GameObject bulletPrefab;
    public Camera playerCamera;

    [Header("Audio")]
    public AudioSource gunAudioSource;
    public float gunFadeOutSpeed = 12f;

    [Header("Muzzle Flash")]
    public ParticleSystem muzzleFlash;

    [Header("Aiming")]
    public float aimDistance = 100f;
    public float gunRotateSpeed = 12f;
    public LayerMask aimMask = ~0;

    [Header("Shooting")]
    public float fireRate = 0.12f;

    private float nextFireTime;
    private Vector3 currentAimPoint;
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

        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        UpdateAimPoint();
        RotateGunYawOnly();

        bool isFiring =
    (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
    (Gamepad.current != null && Gamepad.current.rightShoulder.isPressed);

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

    private void UpdateAimPoint()
    {
        if (playerCamera == null)
            return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, aimDistance, aimMask))
        {
            currentAimPoint = hit.point;
        }
        else
        {
            currentAimPoint = ray.GetPoint(aimDistance);
        }
    }

    private void RotateGunYawOnly()
    {
        if (gunPivot == null)
            return;

        Vector3 direction = currentAimPoint - gunPivot.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        gunPivot.rotation = Quaternion.Slerp(
            gunPivot.rotation,
            targetRotation,
            gunRotateSpeed * Time.deltaTime
        );
    }

    private void Shoot()
    {
        if (Time.time < nextFireTime)
            return;

        if (bulletPrefab == null || gunPoint == null)
            return;

        nextFireTime = Time.time + fireRate;

        Vector3 shootDirection = gunPoint.forward;
        Vector3 spawnPosition = gunPoint.position + shootDirection * 0.8f;

        GameObject bulletObject = Instantiate(
            bulletPrefab,
            spawnPosition,
            Quaternion.LookRotation(shootDirection)
        );

        Bullet bullet = bulletObject.GetComponent<Bullet>();

        if (bullet != null)
        {
            // PASS THE AIM DISTANCE HERE AS THE THIRD ARGUMENT
            bullet.Launch(shootDirection, ownerColliders, aimDistance);
        }

        PlayMuzzleFlash();
    }

    private void PlayMuzzleFlash()
    {
        if (muzzleFlash == null)
            return;

        muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        muzzleFlash.Play();
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