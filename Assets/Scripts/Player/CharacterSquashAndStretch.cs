using System.Collections;
using UnityEngine;

public class CharacterSquashAndStretch : MonoBehaviour
{
    [SerializeField] private Transform targetModel;
    [SerializeField] private float duration = 0.2f;

    private Vector3 originalScale;
    private Coroutine currentCoroutine;

    private void Awake()
    {
        if (targetModel == null) targetModel = transform;
        originalScale = targetModel.localScale;
    }

    public void TriggerJumpSquash()
    {
        // Stretch vertically, squash horizontally
        ApplySquashStretch(new Vector3(0.75f, 1.35f, 0.75f));
    }

    public void TriggerLandSquash()
    {
        // Squash vertically, expand horizontally
        ApplySquashStretch(new Vector3(1.3f, 0.7f, 1.3f));
    }

    private void ApplySquashStretch(Vector3 targetScaleMultiplier)
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(AnimateScale(Vector3.Scale(originalScale, targetScaleMultiplier)));
    }

    private IEnumerator AnimateScale(Vector3 targetScale)
    {
        float elapsed = 0f;
        Vector3 startScale = targetModel.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            targetModel.localScale = Vector3.Lerp(startScale, targetScale, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            targetModel.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            yield return null;
        }

        targetModel.localScale = originalScale;
    }
}
