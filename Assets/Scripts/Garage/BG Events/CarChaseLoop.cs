using UnityEngine;

public class CarStraightChase : MonoBehaviour
{
    [Header("Highway Path")]
    public Vector3 pointA = new Vector3(-40f, 0.2f, -10f);
    public Vector3 pointB = new Vector3(40f, 0.2f, -10f);

    [Header("Loop Timing")]
    public float travelDuration = 8f;
    public float waitTimeAtB = 5f;
    public float waitTimeAtA = 3f;

    [Header("Audio")]
    public AudioSource carAudio;  // Your Ferrari Audio Source is here
    public float audioFadeDuration = 0.5f;

    private enum State { Driving, WaitingAtB, WaitingAtA }
    private State currentState;
    private float stateTimer;
    private float progress;
    private Vector3 forwardDir;
    private float audioTargetVolume = 0f;
    private float audioCurrentVolume = 0f;

    void Start()
    {
        forwardDir = (pointB - pointA).normalized;
        transform.position = pointA;
        transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
        currentState = State.Driving;

        // If assigned, set up audio
        if (carAudio != null)
        {
            carAudio.loop = true;
            carAudio.volume = 0f;
            carAudio.Play();
            audioTargetVolume = 1f;
            // No logs – silent success
        }
        // If not assigned, just skip audio – no error
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Driving:
                progress += Time.deltaTime / travelDuration;
                if (progress >= 1f)
                {
                    progress = 1f;
                    currentState = State.WaitingAtB;
                    stateTimer = 0f;
                    audioTargetVolume = 0f;
                }
                MoveStraight(progress);
                break;

            case State.WaitingAtB:
                stateTimer += Time.deltaTime;
                transform.position = pointB + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.03f);
                transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                if (stateTimer >= waitTimeAtB)
                {
                    transform.position = pointA;
                    progress = 0f;
                    currentState = State.WaitingAtA;
                    stateTimer = 0f;
                    audioTargetVolume = 0f;
                }
                break;

            case State.WaitingAtA:
                stateTimer += Time.deltaTime;
                transform.position = pointA + Vector3.up * (Mathf.Sin(Time.time * 3f + 1f) * 0.03f);
                transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                if (stateTimer >= waitTimeAtA)
                {
                    currentState = State.Driving;
                    progress = 0f;
                    audioTargetVolume = 1f;
                }
                break;
        }

        // Fade audio
        if (carAudio != null)
        {
            audioCurrentVolume = Mathf.MoveTowards(audioCurrentVolume, audioTargetVolume, Time.deltaTime / audioFadeDuration);
            carAudio.volume = audioCurrentVolume;
        }
    }

    void MoveStraight(float t)
    {
        Vector3 targetPos = Vector3.Lerp(pointA, pointB, t);
        transform.position = targetPos;
        transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
    }
}