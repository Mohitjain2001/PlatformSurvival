using System.Collections;
using UnityEngine;

public class PlatformTile : MonoBehaviour
{
    [Header("Tile Settings")]
    [SerializeField] private float delayBeforeFall = 0.9f;
    [SerializeField] private float shakeIntensity = 0.08f;
    [SerializeField] private float fallSpeed = 12f;

    [Header("Visual Feedback Colors")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.8f, 0.9f); // Cyan/Teal
    [SerializeField] private Color warningColor = new Color(1.0f, 0.6f, 0.0f); // Orange
    [SerializeField] private Color dangerColor = new Color(0.9f, 0.1f, 0.1f); // Red

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
            // Instantiate material so color changes don't affect shared material
            tileMaterial = meshRenderer.material;
            tileMaterial.color = normalColor;
        }
    }

    private void Start()
    {
        initialPosition = transform.position;
    }

    public void SetColors(Color normal, Color warning, Color danger)
    {
        normalColor = normal;
        warningColor = warning;
        dangerColor = danger;
        if (tileMaterial != null)
        {
            tileMaterial.color = normalColor;
        }
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

            // Color shift from normal -> warning -> danger
            if (tileMaterial != null)
            {
                if (progress < 0.5f)
                {
                    tileMaterial.color = Color.Lerp(normalColor, warningColor, progress * 2f);
                }
                else
                {
                    tileMaterial.color = Color.Lerp(warningColor, dangerColor, (progress - 0.5f) * 2f);
                }
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
