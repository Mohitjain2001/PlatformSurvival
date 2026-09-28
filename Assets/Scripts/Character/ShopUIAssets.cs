using UnityEngine;

/// <summary>
/// Procedural sprite generator for the Character Shop UI matching reference concept media_1790511363312.png:
/// - "CHARACTER SHOP" 3D Glossy Header Banner with Crown.
/// - Left [<] and Right [>] Blue Navigation Arrow Buttons.
/// - Dark Blue Showroom Name Pill & Green "(✓) EQUIPPED" Badge.
/// - 4x2 Grid Cards with Yellow Selection Border, Lock Badges (🔒), and Green Checkmarks (✓).
/// - Yellow "WATCH AD" and Dark "UNLOCK AT LEVEL X" Action Buttons.
/// </summary>
public static class ShopUIAssets
{
    private static Sprite shopBannerSprite;
    private static Sprite arrowLeftSprite;
    private static Sprite arrowRightSprite;
    private static Sprite redCloseBtnSprite;
    private static Sprite namePillSprite;
    private static Sprite equippedBadgeSprite;
    private static Sprite mainPanelBgSprite;
    private static Sprite selectedCardBgSprite;
    private static Sprite lockedCardBgSprite;
    private static Sprite lockIconSprite;
    private static Sprite checkmarkSprite;
    private static Sprite watchAdBtnSprite;
    private static Sprite levelUnlockBtnSprite;
    private static Sprite goldCoinSprite;

    public static Sprite GetCharacterShopBanner()
    {
        if (shopBannerSprite == null)
            shopBannerSprite = CreateShopBanner(450, 90);
        return shopBannerSprite;
    }

    public static Sprite GetNavArrowLeft()
    {
        if (arrowLeftSprite == null)
            arrowLeftSprite = CreateNavArrow(88, true);
        return arrowLeftSprite;
    }

    public static Sprite GetNavArrowRight()
    {
        if (arrowRightSprite == null)
            arrowRightSprite = CreateNavArrow(88, false);
        return arrowRightSprite;
    }

    public static Sprite GetRedCloseBtn()
    {
        if (redCloseBtnSprite == null)
            redCloseBtnSprite = CreateCloseBtn(88);
        return redCloseBtnSprite;
    }

    public static Sprite GetNamePill()
    {
        if (namePillSprite == null)
            namePillSprite = CreateRoundedBox(260, 54, 16, new Color(0.08f, 0.22f, 0.50f), new Color(0.00f, 0.65f, 1.00f), 3);
        return namePillSprite;
    }

    public static Sprite GetEquippedBadge()
    {
        if (equippedBadgeSprite == null)
            equippedBadgeSprite = CreateRoundedBox(240, 48, 14, new Color(0.10f, 0.82f, 0.25f), Color.white, 2);
        return equippedBadgeSprite;
    }

    public static Sprite GetMainPanelBg()
    {
        if (mainPanelBgSprite == null)
            mainPanelBgSprite = CreateRoundedBox(512, 512, 28, new Color(0.00f, 0.52f, 0.95f), Color.white, 6);
        return mainPanelBgSprite;
    }

    public static Sprite GetSelectedCardBg()
    {
        if (selectedCardBgSprite == null)
            selectedCardBgSprite = CreateRoundedBox(200, 280, 22, new Color(0.15f, 0.52f, 0.92f), new Color(1.00f, 0.82f, 0.10f), 10);
        return selectedCardBgSprite;
    }

    public static Sprite GetLockedCardBg()
    {
        if (lockedCardBgSprite == null)
            lockedCardBgSprite = CreateRoundedBox(200, 280, 22, new Color(0.08f, 0.36f, 0.72f), new Color(0.00f, 0.62f, 0.96f), 4);
        return lockedCardBgSprite;
    }

    public static Sprite GetLockIcon()
    {
        if (lockIconSprite == null)
            lockIconSprite = CreateLockIcon(48);
        return lockIconSprite;
    }

    public static Sprite GetCheckmarkBadge()
    {
        if (checkmarkSprite == null)
            checkmarkSprite = CreateCheckmarkBadge(56);
        return checkmarkSprite;
    }

    public static Sprite GetWatchAdBtn()
    {
        if (watchAdBtnSprite == null)
            watchAdBtnSprite = CreateRoundedBox(440, 100, 24, new Color(1.00f, 0.75f, 0.05f), Color.white, 4);
        return watchAdBtnSprite;
    }

    public static Sprite GetLevelUnlockBtn()
    {
        if (levelUnlockBtnSprite == null)
            levelUnlockBtnSprite = CreateRoundedBox(440, 100, 24, new Color(0.18f, 0.35f, 0.62f), new Color(0.90f, 0.95f, 1.00f), 3);
        return levelUnlockBtnSprite;
    }

    public static Sprite GetGoldCoin()
    {
        if (goldCoinSprite == null)
            goldCoinSprite = CreateCoinSprite(64);
        return goldCoinSprite;
    }

    // ─────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────

    private static Sprite CreateRoundedBox(int width, int height, int cornerRadius, Color fillColor, Color strokeColor, int strokeThickness)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[width * height];

        float r = cornerRadius;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x < r) ? r - x : (x > width - 1 - r) ? x - (width - 1 - r) : 0;
                float dy = (y < r) ? r - y : (y > height - 1 - r) ? y - (height - 1 - r) : 0;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                Color c = Color.clear;
                if (dist <= r)
                {
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);
                    bool isStroke = strokeThickness > 0 && dist > (r - strokeThickness);
                    c = isStroke ? strokeColor : fillColor;
                    c.a *= alpha;
                }
                pixels[y * width + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateShopBanner(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[width * height];

        Color blueGrad1 = new Color(0.00f, 0.65f, 1.00f);
        Color blueGrad2 = new Color(0.00f, 0.40f, 0.85f);
        Color yellowText = new Color(1.00f, 0.85f, 0.10f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float t = (float)y / height;
                Color c = Color.Lerp(blueGrad2, blueGrad1, t);

                // Simple banner border
                if (x < 6 || x > width - 7 || y < 6 || y > height - 7)
                    c = Color.white;

                pixels[y * width + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateNavArrow(int size, bool isLeft)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        float cx = (size - 1) / 2f;
        float cy = (size - 1) / 2f;
        float r = cx * 0.90f;

        Color blueCircle = new Color(0.00f, 0.50f, 0.95f);
        Color border = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                Color c = Color.clear;
                if (d <= r)
                {
                    float alpha = Mathf.Clamp01(r - d + 0.5f);
                    c = (d > r - 4f) ? border : blueCircle;

                    // Arrow symbol (< or >)
                    float arrowX = isLeft ? -dx : dx;
                    if (d < r - 6f && arrowX > -r * 0.4f && arrowX < r * 0.4f)
                    {
                        if (Mathf.Abs(dy - arrowX * 0.8f) < 4f)
                            c = Color.white;
                    }

                    c.a *= alpha;
                }
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateCloseBtn(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        float cx = (size - 1) / 2f;
        float cy = (size - 1) / 2f;
        float r = cx * 0.90f;

        Color redCircle = new Color(0.95f, 0.22f, 0.22f);
        Color whiteBorder = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                Color c = Color.clear;
                if (d <= r)
                {
                    float alpha = Mathf.Clamp01(r - d + 0.5f);
                    c = (d > r - 5f) ? whiteBorder : redCircle;

                    if (d < r - 6f)
                    {
                        if (Mathf.Abs(dx - dy) < 4f || Mathf.Abs(dx + dy) < 4f)
                        {
                            if (Mathf.Abs(dx) < r * 0.5f && Mathf.Abs(dy) < r * 0.5f) c = Color.white;
                        }
                    }
                    c.a *= alpha;
                }
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateLockIcon(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        float cx = (size - 1) / 2f;
        float cy = (size - 1) / 2f;

        Color lockDark = new Color(0.12f, 0.20f, 0.35f, 0.90f);
        Color lockGold = new Color(1.00f, 0.85f, 0.10f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                Color c = Color.clear;

                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= cx * 0.85f)
                {
                    c = lockDark;
                    // Padlock shackle & body
                    if (dy < 0 && Mathf.Abs(dx) < cx * 0.5f && dy > -cy * 0.6f) c = lockGold;
                    if (dy >= 0 && dy < cy * 0.5f)
                    {
                        float dArc = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - cx * 0.3f);
                        if (dArc < 3.5f) c = lockGold;
                    }
                }
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateCheckmarkBadge(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        float center = (size - 1) / 2f;
        float r = center * 0.90f;
        Color greenCircle = new Color(0.12f, 0.85f, 0.32f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                Color c = Color.clear;
                if (d <= r)
                {
                    float alpha = Mathf.Clamp01(r - d + 0.5f);
                    c = greenCircle;

                    if (dx > -12f && dx < 12f && dy > -12f && dy < 12f)
                    {
                        if ((dx < -2f && Mathf.Abs(dy - (dx + 4f)) < 3.5f) ||
                            (dx >= -2f && Mathf.Abs(dy - (-dx * 1.2f - 2f)) < 3.5f))
                        {
                            c = Color.white;
                        }
                    }

                    c.a *= alpha;
                }
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateCoinSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        float cx = (size - 1) / 2f;
        float cy = (size - 1) / 2f;
        float r = cx * 0.92f;

        Color gold1 = new Color(1.00f, 0.85f, 0.10f);
        Color gold2 = new Color(0.90f, 0.60f, 0.00f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                Color c = Color.clear;
                if (d <= r)
                {
                    float alpha = Mathf.Clamp01(r - d + 0.5f);
                    c = (d > r * 0.72f) ? gold2 : gold1;
                    c.a *= alpha;
                }
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
