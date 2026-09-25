using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformTile : MonoBehaviour
{
    [Header("Tile Settings")]
    [SerializeField] private float whiteHoldDuration = 0.28f;
    [SerializeField] private float fadeBeforeFallDuration = 0.18f;
    [SerializeField] private float fallDuration = 0.30f;
    [SerializeField] private float fallDistance = 2.4f;

    [Header("Visual Feedback Colors")]
    [SerializeField] private Color normalColor = new Color(0.90f, 0.38f, 0.48f); // Coral/Pink
    [SerializeField] private Color steppedColor = Color.white; // Pure white when stepped on!
    [SerializeField] private Color fadeColor = new Color(0.86f, 0.66f, 1.0f); // Soft lavender like the reference trail
    [SerializeField] private Color warningColor = new Color(1.0f, 0.6f, 0.0f); // Orange
    [SerializeField] private Color dangerColor = new Color(0.95f, 0.15f, 0.15f); // Red

    private MeshRenderer[] meshRenderers;
    private Collider[] tileColliders;
    private Material[] tileMaterials;

    private Vector3 initialPosition;
    private Vector3 initialScale = Vector3.zero;
    private bool isSteppedOn = false;
    private bool isFalling = false;
    private bool isAvailable = true;

    public bool IsAvailable => isAvailable && !isFalling;
    public bool IsShaking => isSteppedOn && !isFalling;
    public bool IsFalling => isFalling;
    public Vector3 Position => transform.position;

    private void Awake()
    {
        if (initialScale == Vector3.zero)
        {
            initialScale = transform.localScale;
        }

        meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        tileColliders = GetComponentsInChildren<Collider>(true);
        
        if (meshRenderers != null && meshRenderers.Length > 0)
        {
            List<Material> mats = new List<Material>();
            foreach (var mr in meshRenderers)
            {
                if (mr != null)
                {
                    foreach (var mat in mr.materials)
                    {
                        if (mat != null)
                        {
                            mat.enableInstancing = true;
                            mats.Add(mat);
                        }
                    }
                }
            }
            tileMaterials = mats.ToArray();
            SetTileColor(normalColor);
        }
    }

    public void SetTileColor(Color color)
    {
        if (tileMaterials != null)
        {
            foreach (var mat in tileMaterials)
            {
                if (mat == null) continue;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                mat.color = color;
            }
        }
    }

    private void Start()
    {
        initialPosition = transform.position;
        if (initialScale == Vector3.zero)
        {
            initialScale = transform.localScale;
        }
    }

    public void SetInitialPosition(Vector3 pos)
    {
        initialPosition = pos;
        if (initialScale == Vector3.zero)
        {
            initialScale = transform.localScale;
        }
    }

    public void SetInitialScale(Vector3 scale)
    {
        initialScale = scale;
        transform.localScale = scale;
    }

    public void ResetTile()
    {
        StopAllCoroutines();
        isSteppedOn = false;
        isFalling = false;
        isAvailable = true;
        gameObject.SetActive(true);
        if (initialPosition != Vector3.zero)
        {
            transform.position = initialPosition;
        }
        else
        {
            initialPosition = transform.position;
        }

        if (initialScale != Vector3.zero)
        {
            transform.localScale = initialScale;
        }
        else if (transform.localScale != Vector3.zero)
        {
            initialScale = transform.localScale;
        }

        if (tileColliders != null)
        {
            foreach (var col in tileColliders)
            {
                if (col != null) col.enabled = true;
            }
        }
        SetTileColor(normalColor);
    }

    public void SetColors(Color normal, Color warning, Color danger)
    {
        normalColor = normal;
        warningColor = warning;
        dangerColor = danger;
        SetTileColor(normalColor);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckContact(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckContact(other.gameObject);
    }

    private void CheckContact(GameObject target)
    {
        if (isSteppedOn || isFalling) return;

        bool isPlayer = target.GetComponent<PlayerController>() != null;
        bool isBot = target.GetComponent<BotController>() != null;

        if (isPlayer || isBot)
        {
            TriggerFallSequence(isPlayer);
        }
    }

    public void TriggerFallSequence(bool isPlayer = false)
    {
        if (isSteppedOn || isFalling) return;
        StartCoroutine(FallRoutine(isPlayer));
    }

    private IEnumerator FallRoutine(bool isPlayer = false)
    {
        isSteppedOn = true;

        if (isPlayer && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayTileStep();
        }

        // Reference behavior: touched tiles become a crisp white trail immediately.
        SetTileColor(steppedColor);

        float elapsed = 0f;
        Vector3 lockedPosition = transform.position;

        while (elapsed < whiteHoldDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = lockedPosition;
            SetTileColor(steppedColor);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fadeBeforeFallDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeBeforeFallDuration);
            transform.position = lockedPosition;
            SetTileColor(Color.Lerp(steppedColor, fadeColor, progress));
            yield return null;
        }

        isFalling = true;
        isAvailable = false;

        if (isPlayer && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayTileDrop();
        }

        if (tileColliders != null)
        {
            foreach (var col in tileColliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        float fallTimer = 0f;
        Vector3 startScale = (initialScale != Vector3.zero) ? initialScale : transform.localScale;
        Vector3 fallStartPosition = transform.position;
        Vector3 fallEndPosition = fallStartPosition + Vector3.down * fallDistance;

        while (fallTimer < fallDuration)
        {
            fallTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(fallTimer / fallDuration);
            transform.position = Vector3.Lerp(fallStartPosition, fallEndPosition, progress);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
            yield return null;
        }

        Destroy(gameObject);
    }
}
