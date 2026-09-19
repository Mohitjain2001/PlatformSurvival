using System.Collections;
using UnityEngine;

public class PlatformTile : MonoBehaviour
{
    [Header("Tile Settings")]
    [SerializeField] private float delayBeforeFall = 1.15f;
    [SerializeField] private float shakeIntensity = 0.05f;
    [SerializeField] private float fallSpeed = 10.0f;

    [Header("Visual Feedback Colors")]
    [SerializeField] private Color normalColor = new Color(0.90f, 0.38f, 0.48f); // Coral/Pink
    [SerializeField] private Color steppedColor = Color.white; // Pure white when stepped on!
    [SerializeField] private Color warningColor = new Color(1.0f, 0.6f, 0.0f); // Orange
    [SerializeField] private Color dangerColor = new Color(0.95f, 0.15f, 0.15f); // Red

    private MeshRenderer meshRenderer;
    private Collider tileCollider;
    private Material tileMaterial;

    private Vector3 initialPosition;
    private bool isSteppedOn = false;
    private bool isFalling = false;
    private bool isAvailable = true;

    public bool IsAvailable => isAvailable && !isFalling;
    public bool IsShaking => isSteppedOn && !isFalling;
    public bool IsFalling => isFalling;
    public Vector3 Position => transform.position;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        tileCollider = GetComponent<Collider>();
        
        if (meshRenderer != null)
        {
            tileMaterial = meshRenderer.material;
            SetTileColor(normalColor);
        }
    }

    public void SetTileColor(Color color)
    {
        if (tileMaterial == null && meshRenderer != null)
        {
            tileMaterial = meshRenderer.material;
        }
        if (tileMaterial != null)
        {
            if (tileMaterial.HasProperty("_Color")) tileMaterial.SetColor("_Color", color);
            if (tileMaterial.HasProperty("_BaseColor")) tileMaterial.SetColor("_BaseColor", color);
            tileMaterial.color = color;
        }
    }

    private void Start()
    {
        initialPosition = transform.position;
    }

    public void SetInitialPosition(Vector3 pos)
    {
        initialPosition = pos;
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
        transform.localScale = Vector3.one;
        if (tileCollider != null)
        {
            tileCollider.enabled = true;
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

        // Check if collision is coming from player or bot
        if (target.GetComponent<PlayerController>() != null || target.GetComponent<BotController>() != null)
        {
            TriggerFallSequence();
        }
    }

    public void TriggerFallSequence()
    {
        if (isSteppedOn || isFalling) return;
        StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        isSteppedOn = true;
        // Instantly turn solid white when stepped on, matching the reference image!
        SetTileColor(steppedColor);

        float elapsed = 0f;

        while (elapsed < delayBeforeFall)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / delayBeforeFall;

            // Shake offset
            Vector3 shakeOffset = new Vector3(
                Random.Range(-shakeIntensity, shakeIntensity),
                0f,
                Random.Range(-shakeIntensity, shakeIntensity)
            ) * (progress * 1.5f);

            transform.position = initialPosition + shakeOffset;

            // Stay solid white for the majority of the shake duration
            if (progress < 0.70f)
            {
                SetTileColor(steppedColor);
            }
            else
            {
                // Quick red warning flash just before dropping
                float flash = (progress - 0.70f) / 0.30f;
                SetTileColor(Color.Lerp(steppedColor, dangerColor, flash));
            }

            yield return null;
        }

        // Start falling
        isFalling = true;
        isAvailable = false;
        
        if (tileCollider != null)
        {
            tileCollider.enabled = false;
        }

        float fallTimer = 0f;
        Vector3 startScale = transform.localScale;

        while (fallTimer < 2.0f)
        {
            fallTimer += Time.deltaTime;
            transform.position += Vector3.down * (fallSpeed * Time.deltaTime);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, fallTimer / 2.0f);
            yield return null;
        }

        Destroy(gameObject);
    }
}
