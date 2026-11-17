using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// The item bags of both battlers, the item menu, and the items' effects.
/// Item codes: 0 = HP Potion, 1 = MP Potion, 2 = Elixir (both).
/// Each bag is a row of slots; an item is a button object parented to its slot.
/// </summary>
public class ItemHandler : MonoBehaviour
{
    const int BagSize = 25;

    public bool[] PlayerItemisFull;
    public GameObject[] PlayerItemSlots;
    public bool[] EnemyItemisFull;
    public GameObject[] EnemyItemSlots;

    public int SelectedItemID;
    [SerializeField] GameObject PlayerItemGO;
    [SerializeField] GameObject PlayerItemPos;
    [SerializeField] GameObject EnemyItemGO;
    [SerializeField] GameObject EnemyItemPos;

    public IEnumerator itemCoroutine;
    public bool itemActionStarted;
    BattleManager battleManager;

    ItemID itemID;
    public GameObject ItemIGO;
    public float animDuration = 0.5f;
    [SerializeField] GameObject HPpotionBTN;
    [SerializeField] GameObject MPpotionBTN;
    [SerializeField] GameObject ElixirBTN;

    public ParticleSystem healHPParticles;
    public ParticleSystem healMPParticles;

    public TMP_ColorGradient healHPGradientTMP;
    public TMP_ColorGradient healMPGradientTMP;
    public TMP_ColorGradient healBOTHGradientTMP;

    void Start()
    {
        battleManager = FindFirstObjectByType<BattleManager>();
        PlayerItemPos.transform.localScale = Vector2.zero;
    }

    // ---------------------------------------------------------------- Choosing an item

    // Called by each item button in the bag.
    public void OnItemSelected()
    {
        // The button that was just pressed: this is how a shared handler knows which item was picked.
        ItemIGO = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        itemID = ItemIGO.GetComponent<ItemID>();

        // Refuse an item that would do nothing.
        BarAnimation bars = battleManager.PlayerBarAnim;
        bool fullHP = bars.currentHealth >= bars.maxHealth;
        bool fullMP = bars.currentMana >= bars.maxStamina;
        bool wasted = (fullHP && itemID.healHP && !itemID.healMP)
                   || (fullMP && itemID.healMP && !itemID.healHP)
                   || (fullHP && fullMP && itemID.healHP && itemID.healMP);

        if (wasted)
        {
            battleManager.audioSource.PlayOneShot(battleManager.itemError);
            return;
        }

        SelectedItemID = itemID.itemcode;
        battleManager.SelecteditemName = itemID.itemName;
        battleManager.actionTaken = true;
        battleManager.battleState = BattleState.ITEM;
        battleManager.StartCoroutine(battleManager.closeItemCO(true));
        battleManager.BattleLogTrans.transform.localPosition = new Vector3(-751f, -149f, 0f);
    }

    public void StartItemCoroutine()
    {
        switch (SelectedItemID)
        {
            case 0: itemCoroutine = UseItemCO("+300HP", healHPGradientTMP, battleManager.healColor, 300, 0, healHPParticles); break;
            case 1: itemCoroutine = UseItemCO("+30MP", healMPGradientTMP, battleManager.mphealColor, 0, 30, healMPParticles); break;
            case 2: itemCoroutine = UseItemCO("+500HP\n+100MP", healBOTHGradientTMP, battleManager.bothhealColor, 500, 100, healHPParticles, healMPParticles); break;
        }
        StartCoroutine(itemCoroutine);
    }

    // ---------------------------------------------------------------- Item effect

    // Heal the user's HP and/or MP, show the effect, then use up the item.
    IEnumerator UseItemCO(string label, TMP_ColorGradient gradient, Color glow, float hp, float mp, params ParticleSystem[] effects)
    {
        ItemID item = ItemIGO.GetComponent<ItemID>();
        if (itemActionStarted) yield break;
        itemActionStarted = true;

        battleManager.EffectValueText.text = label;
        battleManager.audioSource.PlayOneShot(item.itemSFX);
        battleManager.ShowEffectText(gradient);
        foreach (ParticleSystem effect in effects)
            battleManager.SpawnEffect(effect);

        bool player = battleManager.playerTurn;
        BarAnimation bars = player ? battleManager.PlayerBarAnim : battleManager.EnemyBarAnim;
        if (hp > 0) bars.heal(hp);
        if (mp > 0) bars.regainStamina(mp);
        (player ? PlayerItemisFull : EnemyItemisFull)[item.itemIndex] = false;

        yield return battleManager.GlowCO(glow);

        battleManager.effectValueTextCG.LeanAlpha(0, 0.5f);
        Destroy(ItemIGO);
        itemActionStarted = false;
    }

    // ---------------------------------------------------------------- The bags

    // Put a new item in the first free slot of the owner's bag.
    public void addItem(int itemIndex, GameObject GO)
    {
        bool[] full;
        if (GO.tag == "Player") full = PlayerItemisFull;
        else if (GO.tag == "Enemy") full = EnemyItemisFull;
        else return;

        for (int i = 0; i < BagSize; i++)
        {
            if (full[i]) continue;
            InstantiateItem(itemIndex, i, GO);
            full[i] = true;
            break;
        }
    }

    // Create the item's button in a slot of the owner's bag.
    void InstantiateItem(int itemIndex, int slotIndex, GameObject owner)
    {
        GameObject[] slots;
        if (owner.tag == "Player") slots = PlayerItemSlots;
        else if (owner.tag == "Enemy") slots = EnemyItemSlots;
        else return;

        GameObject prefab = itemIndex == 0 ? HPpotionBTN : itemIndex == 1 ? MPpotionBTN : itemIndex == 2 ? ElixirBTN : null;
        if (prefab == null) return;

        GameObject item = Instantiate(prefab, slots[slotIndex].transform, false);
        item.GetComponent<ItemID>().itemIndex = slotIndex;
    }

    // Close gaps left by used items: each item with an empty slot before it moves forward one slot.
    public void SortItem()
    {
        ShiftItemsForward(PlayerItemisFull, PlayerItemSlots, battleManager.playerGO);
        ShiftItemsForward(EnemyItemisFull, EnemyItemSlots, battleManager.enemyGO);
    }

    void ShiftItemsForward(bool[] full, GameObject[] slots, GameObject owner)
    {
        for (int i = 1; i < full.Length; i++)
        {
            if (!full[i] || full[i - 1]) continue;
            GameObject item = slots[i].transform.GetChild(0).gameObject;
            int code = item.GetComponent<ItemID>().itemcode;
            Destroy(item);
            InstantiateItem(code, i - 1, owner);
            full[i - 1] = true;
            full[i] = false;
        }
    }

    // ---------------------------------------------------------------- Item menu

    public void OpenItemUI()
    {
        PlayerItemPos.transform.LeanScale(Vector2.one, animDuration).setEaseOutBack();
    }

    public void CloseItemUI()
    {
        PlayerItemPos.transform.LeanScale(Vector2.zero, 0.1f).setEaseInBack();
    }
}
