using System.Collections;
using UnityEngine;

/// <summary>
/// Ticks down status effects at the start of a battler's turn and removes the ones that expire.
/// Status effect 0 is Attack Up (from the Attack Up skill): +50% of base attack while it lasts.
/// </summary>
public class StatusEffectSystem : MonoBehaviour
{
    BattleManager battleManager;
    public IEnumerator statusEffectCoroutine;
    public IEnumerator statusEffectCheckCoroutine;
    public bool statusEffectActionStarted;
    public bool statusEffectCheckActionStarted;
    public int ActiveStatusEffectID;
    public bool SEchecked;
    int removedThisCheck; // Effects removed so far in the current check, so later indices shift back.

    public AudioClip statusEffectExpiredSFX;

    void Start()
    {
        battleManager = FindFirstObjectByType<BattleManager>();
    }

    public void StartStatusEffectCoroutine(GameObject effectedGO, int SEindex)
    {
        if (ActiveStatusEffectID == 0) statusEffectCoroutine = AttackUpSECO(effectedGO, SEindex);
        StartCoroutine(statusEffectCoroutine);
    }

    // Tick every status effect on a battler, one after another. Sets BattleManager.seAllChecked when done.
    public void updateStatusEffectState(GameObject effectedGO)
    {
        statusEffectCheckCoroutine = UpdateSECO(effectedGO);
        StartCoroutine(statusEffectCheckCoroutine);
    }

    // Does this battler ("Player" or "Enemy") already have the effect from this skill?
    public bool checkStatusEffects(int skillCode, string battlerType)
    {
        CharaStats stats = battlerType == "Player" ? battleManager.playerStats
                         : battlerType == "Enemy" ? battleManager.enemyStats
                         : null;
        return stats != null && stats.StatusEffects.Exists(e => e.StatusEffectSkillCode == skillCode);
    }

    IEnumerator UpdateSECO(GameObject effectedGO)
    {
        CharaStats charaStats = effectedGO.GetComponent<CharaStats>();
        int count = charaStats.StatusEffects.Count;
        removedThisCheck = 0;
        if (statusEffectCheckActionStarted) yield break;
        statusEffectCheckActionStarted = true;

        for (int i = 0; i < count; i++)
        {
            SEchecked = false;
            StartStatusEffectCoroutine(effectedGO, i - removedThisCheck);
            while (!SEchecked) yield return null;
        }

        battleManager.seAllChecked = true;
        statusEffectCheckActionStarted = false;
    }

    // Count Attack Up down by a turn, or remove it and announce it when it runs out.
    IEnumerator AttackUpSECO(GameObject effectedGO, int SEindex)
    {
        CharaStats charaStats = effectedGO.GetComponent<CharaStats>();
        StatusEffectSO statusEffectSO = charaStats.StatusEffects[SEindex];
        if (statusEffectActionStarted) yield break;
        statusEffectActionStarted = true;

        if (statusEffectSO.Counter > 0)
        {
            statusEffectSO.Counter--;
        }
        else
        {
            battleManager.audioSource.PlayOneShot(statusEffectExpiredSFX, 0.5f);
            charaStats.C_AttackValue = Mathf.Max(charaStats.C_AttackValue - charaStats.Init_AttackValue * 0.5f, 0);
            charaStats.StatusEffects.RemoveAt(SEindex);
            removedThisCheck++;

            battleManager.BattleLogText.text = charaStats.CharacterName + "'s Attack Up effect \nwore off";
            battleManager.BattleLogTrans.LeanMoveLocalX(0f, 0.2f).setEaseOutBack();
            yield return new WaitForSeconds(2f);
            battleManager.BattleLogTrans.LeanMoveLocalX(760f, 0.5f).setEaseInBack();
            yield return new WaitForSeconds(0.5f);
            battleManager.BattleLogTrans.transform.localPosition = new Vector3(-751f, 0f, 0f);
        }

        SEchecked = true;
        statusEffectActionStarted = false;
    }
}
