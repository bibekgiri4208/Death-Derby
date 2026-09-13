using System.Collections;
using UnityEngine;

public class SurvivorGarageAnimation : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Ambient Waving")]
    [SerializeField] private string wavingState = "Waving";
    [Min(0f)]
    [SerializeField] private float wavingGapSeconds = 0.5f;
    [Min(0.5f)]
    [SerializeField] private float fallbackStateDuration = 3f;

    [Header("Click Interactions")]
    [SerializeField] private string lookAroundState = "LookAround";
    [SerializeField] private string danceState = "Dance";

    [Header("Click To Interact")]
    [SerializeField] private bool clickableToInteract = true;

    private Coroutine ambientRoutine;
    private Coroutine interactionRoutine;

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
        if (interactionRoutine != null)
        {
            StopCoroutine(interactionRoutine);
            interactionRoutine = null;
        }
    }

    private IEnumerator AmbientLoop()
    {
        while (enabled)
        {
            yield return PlayState(wavingState);
            yield return new WaitForSeconds(wavingGapSeconds);
        }
    }

    private IEnumerator PlayState(string state)
    {
        if (animator == null) yield break;

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

    public void PlayRandomInteraction()
    {
        if (animator == null) return;

        if (interactionRoutine != null)
            StopCoroutine(interactionRoutine);
        if (ambientRoutine != null)
            StopCoroutine(ambientRoutine);

        string state = Random.value < 0.5f ? lookAroundState : danceState;
        interactionRoutine = StartCoroutine(PlayInteractionRoutine(state));
    }

    private IEnumerator PlayInteractionRoutine(string state)
    {
        yield return PlayState(state);
        interactionRoutine = null;
        ambientRoutine = StartCoroutine(AmbientLoop());
    }

    private void OnMouseDown()
    {
        if (clickableToInteract)
            PlayRandomInteraction();
    }
}