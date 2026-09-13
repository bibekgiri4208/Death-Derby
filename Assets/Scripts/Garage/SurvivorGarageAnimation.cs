using System.Collections;
using UnityEngine;

public class SurvivorGarageAnimation : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Ambient Animations")]
    [SerializeField] private string lookAroundState = "LookAround";
    [SerializeField] private string wavingState = "Waving";
    [Min(0f)]
    [SerializeField] private float idleGapSeconds = 0.5f;
    [Min(0.5f)]
    [SerializeField] private float fallbackStateDuration = 3f;

    [Header("Dance")]
    [SerializeField] private string danceState = "Dance";

    [Header("Click To Dance")]
    [Tooltip("Clicking the Survivor itself triggers the dance. If false, call PlayDance() from a button instead.")]
    [SerializeField] private bool clickableToDance = true;

    private Coroutine ambientRoutine;
    private Coroutine danceRoutine;
    private string lastPlayedState;
    private bool isDancing;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        ambientRoutine = StartCoroutine(AmbientLoop());
    }

    private void OnDisable()
    {
        if (ambientRoutine != null)
        {
            StopCoroutine(ambientRoutine);
            ambientRoutine = null;
        }
        if (danceRoutine != null)
        {
            StopCoroutine(danceRoutine);
            danceRoutine = null;
        }
        isDancing = false;
    }

    private IEnumerator AmbientLoop()
    {
        while (enabled)
        {
            if (!isDancing)
            {
                string nextState = lastPlayedState == lookAroundState ? wavingState : lookAroundState;
                yield return PlayState(nextState);
                yield return new WaitForSeconds(idleGapSeconds);
            }
            else
            {
                yield return null;
            }
        }
    }

    private IEnumerator PlayState(string state)
    {
        if (animator == null) yield break;

        lastPlayedState = state;
        animator.Play(state, 0, 0f);
        yield return null;
        yield return new WaitForSeconds(GetStateDuration(state));
    }

    private float GetStateDuration(string state)
    {
        if (animator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(state) && info.length > 0f)
                return info.length;
        }
        return fallbackStateDuration;
    }

    public void PlayDance()
    {
        if (animator == null) return;

        if (danceRoutine != null)
            StopCoroutine(danceRoutine);
        isDancing = true;
        danceRoutine = StartCoroutine(PlayDanceRoutine());
    }

    private IEnumerator PlayDanceRoutine()
    {
        yield return PlayState(danceState);
        yield return new WaitForSeconds(idleGapSeconds);
        isDancing = false;
        danceRoutine = null;
    }

    private void OnMouseDown()
    {
        if (clickableToDance)
            PlayDance();
    }
}