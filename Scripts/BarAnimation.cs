using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// A battler's HP, MP and Burst bars. Each bar has a front fill and a back fill: on damage the
/// front drops at once and the back "chips" down after a short pause; on healing the back jumps up
/// and the front fills to meet it.
/// </summary>
public class BarAnimation : MonoBehaviour
{
    public float maxHealth = 5;
    public float currentHealth;
    public float maxStamina = 10;   // Max MP.
    public float currentMana;
    public float maxBurst = 5;
    public float currentBurst;
    public TextMeshProUGUI nameText;

    CharaStats charaStats;

    // Shared by all three bars: how long since the last change, and whether the chip is still paused.
    float lerpTimer;
    float redHpPauseTimer;
    [SerializeField] float redHpPauseTime = 2f; // Pause before a bar chips down (and before HP/Burst fill up).
    [SerializeField] float stPauseTime;         // Pause before the MP bar fills up.
    public float chipSpeed = 2f;
    bool barIsPausing;

    [SerializeField] Image frontHealthBar;
    [SerializeField] Image backHealthBar;
    [SerializeField] TextMeshProUGUI HPtext;
    [SerializeField] Image frontStaminaBar;
    [SerializeField] Image backStaminaBar;
    [SerializeField] TextMeshProUGUI MPtext;
    [SerializeField] Image frontBurstBar;
    [SerializeField] Image backBurstBar;

    // Last values written to the HP/MP labels, so Update only rebuilds a string when the
    // number on screen actually changes instead of once per frame per battler.
    int shownHealth = int.MinValue;
    int shownMaxHealth = int.MinValue;
    int shownMana = int.MinValue;
    int shownMaxStamina = int.MinValue;

    void Awake()
    {
        charaStats = GetComponent<CharaStats>();

        // Each battler's stats panel holds its HP, MP and Burst groups in order; on the enemy's
        // panel they start one child later.
        if (CompareTag("Player")) BindBars(GameObject.FindGameObjectWithTag("PlayerStats").transform, 1);
        else if (CompareTag("Enemy")) BindBars(GameObject.FindGameObjectWithTag("EnemyStats").transform, 2);
    }

    void BindBars(Transform panel, int hpGroup)
    {
        Transform hp = panel.GetChild(hpGroup), mp = panel.GetChild(hpGroup + 1), burst = panel.GetChild(hpGroup + 2);
        frontHealthBar = hp.GetChild(2).GetComponent<Image>();
        backHealthBar = hp.GetChild(1).GetComponent<Image>();
        nameText = hp.GetChild(3).GetComponent<TextMeshProUGUI>();
        HPtext = hp.GetChild(2).GetChild(0).GetComponent<TextMeshProUGUI>();
        frontStaminaBar = mp.GetChild(2).GetComponent<Image>();
        backStaminaBar = mp.GetChild(1).GetComponent<Image>();
        MPtext = mp.GetChild(2).GetChild(0).GetComponent<TextMeshProUGUI>();
        frontBurstBar = burst.GetChild(3).GetComponent<Image>();
        backBurstBar = burst.GetChild(2).GetComponent<Image>();
    }

    void Start()
    {
        charaStats.C_maxHP = charaStats.Init_maxHP;
        charaStats.C_maxCP = charaStats.Init_maxCP;
        charaStats.C_maxBP = maxBurst;
        maxHealth = charaStats.Init_maxHP;
        maxStamina = charaStats.Init_maxCP;
        currentHealth = maxHealth;
        currentMana = maxStamina;
        nameText.text = charaStats.CharacterName;
    }

    void Update()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        currentMana = Mathf.Clamp(currentMana, 0, maxStamina);
        currentBurst = Mathf.Clamp(currentBurst, 0, maxBurst);
        UpdateHealthUI();
        UpdateStaminaUI();
        UpdateBurstUI();
    }

    // ---------------------------------------------------------------- Bar animation

    // Move a bar towards `fraction`: the leading fill snaps, the trailing one catches up.
    void AnimateBar(Image front, Image back, float fraction, float gainPause)
    {
        float front0 = front.fillAmount, back0 = back.fillAmount;

        if (back0 > fraction) // Going down
        {
            front.fillAmount = fraction;
            Chip(back, back0, fraction, redHpPauseTime);
        }
        if (front0 < fraction) // Going up
        {
            back.fillAmount = fraction;
            Chip(front, front0, fraction, gainPause);
        }
    }

    // After the pause, ease the trailing fill towards the target (accelerating).
    void Chip(Image bar, float from, float to, float pause)
    {
        redHpPauseTimer += Time.deltaTime;
        if (barIsPausing && redHpPauseTimer > pause) barIsPausing = false;
        if (barIsPausing) return;

        lerpTimer += Time.deltaTime;
        float t = lerpTimer / chipSpeed;
        bar.fillAmount = Mathf.Lerp(from, to, t * t);
    }

    // Restart the chip after any change.
    void BarChanged()
    {
        lerpTimer = 0f;
        redHpPauseTimer = 0f;
        barIsPausing = true;
    }

    // ---------------------------------------------------------------- HP

    public void UpdateHealthUI()
    {
        int hp = Mathf.RoundToInt(currentHealth);
        int hpMax = Mathf.RoundToInt(maxHealth);
        if (hp != shownHealth || hpMax != shownMaxHealth)
        {
            shownHealth = hp;
            shownMaxHealth = hpMax;
            HPtext.text = hp.ToString() + "/" + hpMax.ToString();
        }
        AnimateBar(frontHealthBar, backHealthBar, currentHealth / maxHealth, redHpPauseTime);
    }

    public void TakeDamage(float damage) { currentHealth -= damage; BarChanged(); }
    public void heal(float hpRecover) { currentHealth += hpRecover; BarChanged(); }

    // ---------------------------------------------------------------- MP

    public void UpdateStaminaUI()
    {
        int mp = Mathf.RoundToInt(currentMana);
        int mpMax = Mathf.RoundToInt(maxStamina);
        if (mp != shownMana || mpMax != shownMaxStamina)
        {
            shownMana = mp;
            shownMaxStamina = mpMax;
            MPtext.text = mp.ToString() + "/" + mpMax.ToString();
        }
        AnimateBar(frontStaminaBar, backStaminaBar, currentMana / maxStamina, stPauseTime);
        if (currentMana > maxStamina) currentMana = maxStamina;
    }

    public void reduceStamina(float energy) { currentMana -= energy; BarChanged(); }
    public void regainStamina(float stRecover) { currentMana += stRecover; BarChanged(); }

    // ---------------------------------------------------------------- Burst

    public void UpdateBurstUI()
    {
        AnimateBar(frontBurstBar, backBurstBar, currentBurst / maxBurst, redHpPauseTime);
    }

    public void reduceBurst(float burstValue) { currentBurst -= burstValue; BarChanged(); }
    public void gainBurst(float burstValue) { currentBurst += burstValue; BarChanged(); }
}
