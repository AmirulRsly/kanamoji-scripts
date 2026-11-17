using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// The skill menu and the skills' effects. Skill codes: 0 = Fireball, 1 = Attack Up, 2 = Cure.
/// How strong a skill is depends on how well the caster did in the skill's question rounds
/// (BattleManager.SkillPerformanceValue: 50, 100 or 150).
/// </summary>
public class SkillSystem : MonoBehaviour
{
    BattleManager battleManager;
    skillID skillID;

    public Transform PlayerSkillUITrans;
    public GameObject PlayerSkillUIGO;
    public GameObject PlayerSkillPanelBox;
    public float animDuration = 0.5f;
    public int SelectedSkillID;
    public IEnumerator skillCoroutine;
    public bool skillActionStarted;
    public GameObject SkillIGO;
    public List<GameObject> SkillBTN = new List<GameObject>();

    public TMP_ColorGradient fireGradientTMP;
    public TMP_ColorGradient healGradientTMP;
    public TMP_ColorGradient normalGradientTMP;
    public TMP_ColorGradient atkUpGradientTMP;

    public ParticleSystem cureParticles;
    public ParticleSystem attackUpParticles;

    void Start()
    {
        battleManager = FindFirstObjectByType<BattleManager>();
        PlayerSkillUITrans.localScale = Vector3.zero;
    }

    // ---------------------------------------------------------------- Choosing a skill

    // Called by each skill button in the skill menu.
    public void OnSkillSelected()
    {
        // The button that was just pressed: this is how a shared handler knows which skill was picked.
        SkillIGO = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
        skillID = SkillIGO.GetComponent<skillID>();
        BarAnimation playerBars = battleManager.PlayerBarAnim;
        bool notEnoughMana = playerBars.currentMana < skillID.skillMPcost;

        if (skillID.skillType == 0) // Damage
        {
            if (notEnoughMana) RejectSkill();
            else
            {
                battleManager.elementType = skillID.skillElement;
                UseSkill();
            }
        }
        else if (skillID.skillType == 1) // Healing
        {
            bool alreadyFull = playerBars.currentHealth >= playerBars.maxHealth && skillID.healHP;
            if (alreadyFull || notEnoughMana) RejectSkill();
            else UseSkill();
        }
        else if (skillID.skillType == 2) // Buff
        {
            bool alreadyBuffed = battleManager.playerStats.StatusEffects.Exists(e => e.StatusEffectSkillCode == skillID.skillCode);
            if (notEnoughMana || alreadyBuffed) RejectSkill();
            else UseSkill();
        }
    }

    void RejectSkill()
    {
        battleManager.audioSource.PlayOneShot(battleManager.itemError);
    }

    // Lock in the selected skill and hand over to the battle.
    void UseSkill()
    {
        SelectedSkillID = skillID.skillCode;
        battleManager.SelectedskillName = skillID.skillName;
        battleManager.SelectedSkillType = skillID.skillType;
        battleManager.actionTaken = true;
        battleManager.battleState = BattleState.SKILL;
        battleManager.StartCoroutine(battleManager.closeSkillCO(true));
        battleManager.BattleLogTrans.transform.localPosition = new Vector3(-751f, -149f, 0f);
    }

    public void startSkillCoroutine()
    {
        switch (SelectedSkillID)
        {
            case 0: skillCoroutine = FireBallCO(); break;
            case 1: skillCoroutine = AttackUpCO(); break;
            case 2: skillCoroutine = CureCO(); break;
        }
        StartCoroutine(skillCoroutine);
    }

    // ---------------------------------------------------------------- Skill effects

    // Fireball's damage is dealt by the battle's normal attack resolution; this only sets its element.
    IEnumerator FireBallCO() // Skill 0
    {
        if (skillActionStarted) yield break;
        skillActionStarted = true;
        battleManager.elementType = 1;
        skillActionStarted = false;
    }

    // Raises the caster's attack by half its base value for 1-3 turns, depending on performance.
    IEnumerator AttackUpCO() // Skill 1
    {
        skillID skill = SkillIGO.GetComponent<skillID>();
        if (skillActionStarted) yield break;
        skillActionStarted = true;

        int turns = 0;
        if (battleManager.SkillPerformanceValue == 50) turns = 1;
        else if (battleManager.SkillPerformanceValue == 100) turns = 2;
        else if (battleManager.SkillPerformanceValue == 150) turns = 3;
        if (turns > 0)
            battleManager.EffectValueText.text = "Attack+\n(" + turns + (turns == 1 ? "turn)" : "turns)");

        battleManager.audioSource.PlayOneShot(skill.skillSFX, 0.5f);
        battleManager.ShowEffectText(atkUpGradientTMP);

        GameObject caster = battleManager.playerTurn ? battleManager.playerGO : battleManager.enemyGO;
        CharaStats stats = caster.GetComponent<CharaStats>();
        battleManager.SpawnEffect(attackUpParticles);

        StatusEffectSO effect = ScriptableObject.CreateInstance<StatusEffectSO>();
        effect.Counter = turns;
        effect.StatusEffectCode = 0;
        effect.StatusEffectSkillCode = 1;
        stats.StatusEffects.Add(effect);
        stats.C_AttackValue += stats.Init_AttackValue * 0.5f;

        yield return battleManager.GlowCO(battleManager.atkUpColor);

        battleManager.effectValueTextCG.LeanAlpha(0, 0.5f);
        skillActionStarted = false;
    }

    // Heals the caster: 400 HP at 100% performance, scaled by performance.
    IEnumerator CureCO() // Skill 2
    {
        skillID skill = SkillIGO.GetComponent<skillID>();
        if (skillActionStarted) yield break;
        skillActionStarted = true;

        float healAmount = 400 * (battleManager.SkillPerformanceValue / 100);
        battleManager.EffectValueText.text = "+" + healAmount + "HP";
        battleManager.audioSource.PlayOneShot(skill.skillSFX);
        battleManager.ShowEffectText(healGradientTMP);

        battleManager.SpawnEffect(cureParticles);
        (battleManager.playerTurn ? battleManager.PlayerBarAnim : battleManager.EnemyBarAnim).heal(healAmount);

        yield return battleManager.GlowCO(battleManager.healColor);

        battleManager.effectValueTextCG.LeanAlpha(0, 0.5f);
        skillActionStarted = false;
    }

    // ---------------------------------------------------------------- Skill menu

    // The skill set is authored on each battler's CharaSkills component, so this only
    // builds the UI for it. Re-adding skillIndex here would duplicate the caller's list.
    public void addSkill(int skillIndex, GameObject GO)
    {
        if (GO.tag == "Player")
            Instantiate(SkillBTN[skillIndex], PlayerSkillPanelBox.transform, false);
    }

    public void OpenSkillUI()
    {
        PlayerSkillUITrans.transform.LeanScale(Vector2.one, animDuration).setEaseOutBack();
    }

    public void CloseSkillUI()
    {
        PlayerSkillUITrans.transform.LeanScale(Vector2.zero, 0.1f).setEaseInBack();
    }
}
