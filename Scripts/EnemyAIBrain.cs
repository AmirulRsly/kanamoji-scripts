using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The enemy's states, in the order a turn usually moves through them.
/// </summary>
public enum EnemyAIState
{
    SETUP,              // Getting ready for battle.
    IDLE,               // Waiting for the player to act.
    MEMORISE,           // Studying the lesson's kana at the start of its turn.
    CHOOSINGACTION,     // Picking an action: item, skill or attack.
    READY,              // Waiting for a question to appear.
    IDENTIFYQUESTION,   // Reading which kana is being asked.
    EVALUATEANSWERS,    // Looking at each answer option in turn.
    ANSWERQUESTION,     // Committing to the chosen answer.
    TOMEMORIZELATER,    // Noting kana it got wrong, to study them next turn.
    DEAD                // Defeated.
}

/// <summary>
/// The enemy that learns kana alongside the player and races them to answer.
///
/// Knowledge model: every kana has a knowledge score (0 to <see cref="maxKanamojiKnowledge"/>).
/// When an answer option appears, the AI rolls 0-50 against that score. Under the score it
/// recognises the kana; over it, it guesses. Knowledge also sets its reaction time: the better
/// it knows the asked kana, the faster it answers. Each turn it studies the lesson a little,
/// and studies harder the kana it recently got wrong, so it improves over the battle.
/// </summary>
public class EnemyAIBrain : MonoBehaviour
{
    // Skill code for Cure, matching skillID.skillCode and the SkillSystem.SkillBTN order.
    const int CureSkillCode = 2;

    // The recognition roll is out of 50, and reaction time is scaled against 52.
    const float RecognitionRollMax = 50f;
    const float ReactionScale = 52f;

    [Header("State")]
    public EnemyAIState enemyAIState;

    [Header("Answering (runtime, shown for debugging)")]
    public List<bool> KanamojiAnswerVerdict = new List<bool>(); // Per answer option: does the AI believe it is correct?
    public float timeToDecideAnswer = 0f;
    public float totalTimeToAnswer = 0f;
    public int AIchosenAnswer = 0;
    public bool AIgotAnswerEarly = false;
    public float AIAnswerDecider = 0f;
    public float AitimetoReact;

    [Header("Knowledge")]
    public List<float> KanamojiKnowledge = new List<float>(); // Knowledge score for every kana.
    public List<int> KanamojiToStudy = new List<int>();       // Kana answered wrong, to study harder next turn.
    public int KanamojiFirstIndex;                             // First kana of the current lesson.
    public int KanamojiLastIndex;                              // One past the last kana of the current lesson.

    [Header("Items in the bag (counted each turn)")]
    public int healHPItemCount = 0;
    public int healMPItemCount = 0;

    [Header("Tuning")]
    public float startingKanamojiKnowledge; // Starting knowledge for the current lesson's kana.
    public float maxKanamojiKnowledge;      // Knowledge cap.
    public float maxStudyRate;              // Most knowledge gained for a wrongly answered kana per turn.
    public float minStudyRate;              // Least knowledge gained for a wrongly answered kana per turn.
    public float maxStudyCount;             // How many wrongly answered kana it keeps studying at once.

    // The AI's decisions are logged for data collection. Turn this off for timing or
    // frame-rate runs: each line builds a string and captures a stack trace.
    public bool logAIDecisions = true;

    int trueAnswerCount = 0;
    int questionIndex;
    bool evaluating; // An answer-evaluation coroutine is running.

    BattleManager battleManager;
    ItemHandler itemHandler;
    SkillSystem skillSystem;
    CharaSkills charaSkills;
    PersistData persistData;

    void Start()
    {
        battleManager = FindFirstObjectByType<BattleManager>();
        itemHandler = FindFirstObjectByType<ItemHandler>();
        skillSystem = FindFirstObjectByType<SkillSystem>();
        charaSkills = GetComponent<CharaSkills>();
        persistData = FindFirstObjectByType<PersistData>();
        enemyAIState = EnemyAIState.SETUP;
    }

    void Update()
    {
        switch (enemyAIState)
        {
            case EnemyAIState.IDLE:
                AitimetoReact = 0f;
                break;

            case EnemyAIState.MEMORISE:
                Memorise();
                break;

            case EnemyAIState.CHOOSINGACTION:
                AIcheckItem();
                ChooseAction();
                break;

            case EnemyAIState.READY:
                if (battleManager.questionIsGiven)
                    enemyAIState = EnemyAIState.IDENTIFYQUESTION;
                break;

            case EnemyAIState.IDENTIFYQUESTION:
                questionIndex = battleManager.CorrectAnswerIndex;
                if (battleManager.answersOptionShown)
                    enemyAIState = EnemyAIState.EVALUATEANSWERS;
                break;

            case EnemyAIState.EVALUATEANSWERS:
                if (!evaluating)
                {
                    if (battleManager.battleAttack)
                        StartCoroutine(EvaluateAttackAnswersCO());
                    else if (battleManager.battleSkill)
                        StartCoroutine(EvaluateSkillAnswersCO());
                }
                break;

            case EnemyAIState.ANSWERQUESTION:
                AnswerQuestion();
                break;

            case EnemyAIState.TOMEMORIZELATER:
                NoteKanaToStudy();
                break;

            case EnemyAIState.DEAD:
                battleManager.enemyAnim.SetBool("isDead", true);
                break;
        }
    }

    // ---------------------------------------------------------------- Learning

    // Knowledge only grows, so it is capped as it increases.
    void GainKnowledge(int kanamojiIndex, float amount)
    {
        KanamojiKnowledge[kanamojiIndex] = Mathf.Min(KanamojiKnowledge[kanamojiIndex] + amount, maxKanamojiKnowledge);
    }

    // Start of the enemy's turn: study the whole lesson a little, and the kana it got wrong a lot.
    void Memorise()
    {
        for (int i = KanamojiFirstIndex; i < KanamojiLastIndex; i++)
            GainKnowledge(i, UnityEngine.Random.Range(1f, 2f));

        for (int i = 0; i < KanamojiToStudy.Count; i++)
        {
            // The most recent mistake is studied hardest; older ones a little less.
            bool latest = i == KanamojiToStudy.Count - 1;
            float gained = latest
                ? UnityEngine.Random.Range(minStudyRate, maxStudyRate)
                : UnityEngine.Random.Range(minStudyRate - 2, maxStudyRate - 2);
            GainKnowledge(KanamojiToStudy[i], gained);
        }

        // Only keep studying the most recent few mistakes.
        if (KanamojiToStudy.Count >= maxStudyCount)
            KanamojiToStudy.RemoveAt(0);

        enemyAIState = EnemyAIState.CHOOSINGACTION;
    }

    // After a question it missed (or didn't answer in time), remember the kana to study next turn.
    void NoteKanaToStudy()
    {
        if (battleManager.battleAttack)
        {
            if (!battleManager.enemyAnswer || !battleManager.enemyAnswerCorrect)
                KanamojiToStudy.Add(questionIndex);
            enemyAIState = EnemyAIState.IDLE;
        }
        else if (battleManager.battleSkill)
        {
            KanamojiToStudy.Add(questionIndex);
            enemyAIState = EnemyAIState.IDLE;
        }
    }

    // ---------------------------------------------------------------- Choosing an action

    // Count the healing items in the enemy's bag.
    public void AIcheckItem()
    {
        healHPItemCount = 0;
        healMPItemCount = 0;
        for (int i = 0; i < itemHandler.EnemyItemSlots.Length; i++)
        {
            if (!itemHandler.EnemyItemisFull[i]) continue;
            ItemID item = itemHandler.EnemyItemSlots[i].transform.GetChild(0).GetComponent<ItemID>();
            if (item.healHP) healHPItemCount++;
            if (item.healMP) healMPItemCount++;
        }
    }

    // Priority: heal HP at half health, restore MP when low, cast Cure at a quarter health, otherwise attack.
    void ChooseAction()
    {
        battleManager.calculateKnowledgeLevel(true);
        BarAnimation bars = battleManager.EnemyBarAnim;

        if (bars.currentHealth <= bars.maxHealth / 2 && healHPItemCount > 0)
        {
            SelectItem(item => item.healHP);
            CommitAction(BattleState.ITEM, EnemyAIState.IDLE);
        }
        else if (bars.currentMana < 20 && healMPItemCount > 0)
        {
            SelectItem(item => item.healMP);
            CommitAction(BattleState.ITEM, EnemyAIState.IDLE);
        }
        else if (bars.currentHealth <= bars.maxHealth / 4 && bars.currentMana >= 20
                 && charaSkills.currentSkillSetCode.Contains(CureSkillCode))
        {
            skillSystem.SkillIGO = skillSystem.SkillBTN[CureSkillCode];
            skillSystem.SelectedSkillID = CureSkillCode;
            battleManager.SelectedskillName = skillSystem.SkillBTN[CureSkillCode].GetComponent<skillID>().skillName;
            CommitAction(BattleState.SKILL, EnemyAIState.READY);
        }
        else
        {
            battleManager.AtkQuestionCount = battleManager.QuestionCountModifier;
            battleManager.elementType = 0;
            battleManager.actionTaken = true;
            battleManager.battleState = BattleState.ATTACK;
            enemyAIState = EnemyAIState.READY;
        }
    }

    // Pick the first item in the enemy's bag that matches.
    void SelectItem(Func<ItemID, bool> matches)
    {
        for (int i = 0; i < itemHandler.EnemyItemSlots.Length; i++)
        {
            if (!itemHandler.EnemyItemisFull[i]) continue;
            GameObject itemObject = itemHandler.EnemyItemSlots[i].transform.GetChild(0).gameObject;
            ItemID item = itemObject.GetComponent<ItemID>();
            if (!matches(item)) continue;
            itemHandler.ItemIGO = itemObject;
            itemHandler.SelectedItemID = item.itemcode;
            battleManager.SelecteditemName = item.itemName;
            return;
        }
    }

    // Hand the chosen item or skill action to the battle.
    void CommitAction(BattleState battleState, EnemyAIState nextState)
    {
        battleManager.BattleLogTrans.transform.localPosition = new Vector3(-751f, -149f, 0f);
        battleManager.actionTaken = true;
        battleManager.battleState = battleState;
        enemyAIState = nextState;
    }

    // ---------------------------------------------------------------- Answering

    // Decide whether one answer option is the asked kana, and how long that takes.
    void RecogniseOption(int kanamojiIndex, int optionIndex)
    {
        AIAnswerDecider = UnityEngine.Random.Range(0, (int)RecognitionRollMax);

        if (AIAnswerDecider <= KanamojiKnowledge[kanamojiIndex])
        {
            // It knows this kana, so it can tell whether it matches the question.
            if (kanamojiIndex == questionIndex)
            {
                KanamojiAnswerVerdict[optionIndex] = true;
                AIchosenAnswer = kanamojiIndex;

                // It recognised the right answer: drop any options it had guessed at earlier.
                for (int i = 0; i < KanamojiAnswerVerdict.Count; i++)
                    if (i != optionIndex) KanamojiAnswerVerdict[i] = false;

                trueAnswerCount = 0;
                AIgotAnswerEarly = true;
            }
            else
            {
                KanamojiAnswerVerdict[optionIndex] = false;
            }
        }
        else
        {
            // It doesn't know this kana, so it guesses: it believes the option 2 times out of 3.
            bool believes = UnityEngine.Random.Range(0, 3) < 2;
            KanamojiAnswerVerdict[optionIndex] = believes;
            if (believes) AIchosenAnswer = kanamojiIndex;
        }

        // Knowing the asked kana well makes it react faster.
        float reactTime = UnityEngine.Random.Range(KanamojiKnowledge[questionIndex], RecognitionRollMax);
        timeToDecideAnswer = ((ReactionScale - reactTime) / ReactionScale) * (battleManager.reactionTime * 2f);

        // If the player has already answered, it hurries.
        if (battleManager.currentTime <= battleManager.reactionTime && battleManager.playerAnswer)
            timeToDecideAnswer /= 2;

        totalTimeToAnswer += timeToDecideAnswer; // Recorded for testing.
    }

    // Several options were believed correct: keep the one whose kana it knows best.
    void KeepBestKnownAnswer()
    {
        for (int i = 0; i < KanamojiAnswerVerdict.Count; i++)
        {
            if (!KanamojiAnswerVerdict[i]) continue;
            if (KanamojiKnowledge[AIchosenAnswer] < KanamojiKnowledge[battleManager.CurrentAnswers[i]])
                AIchosenAnswer = battleManager.CurrentAnswers[i];
            else
                KanamojiAnswerVerdict[i] = false;
        }
        trueAnswerCount = 1;
    }

    void ResetAnswerState()
    {
        totalTimeToAnswer = 0;
        trueAnswerCount = 0;
        AIchosenAnswer = 0;
        AIgotAnswerEarly = false;
        KanamojiAnswerVerdict.Clear();
        for (int i = 0; i < battleManager.CurrentAnswers.Count; i++)
            KanamojiAnswerVerdict.Add(false);
    }

    void LogOption(int i)
    {
        if (!logAIDecisions) return;
        int kana = battleManager.CurrentAnswers[i];
        Debug.Log(i + "Recognising: " + persistData.HiraganaCharacters[kana]
                  + "| DeciderValue: " + AIAnswerDecider.ToString("0.0")
                  + "| Knowledge: " + KanamojiKnowledge[kana].ToString("0.0")
                  + "| Result: " + KanamojiAnswerVerdict[i]
                  + "| TimeTaken: " + timeToDecideAnswer.ToString("0.0"));
    }

    // Attack questions: go through the options until it believes one, or time runs out.
    IEnumerator EvaluateAttackAnswersCO()
    {
        evaluating = true;
        ResetAnswerState();
        bool recognising = true;
        yield return new WaitForSeconds(1f);

        bool done = false;
        while (!done)
        {
            if (!battleManager.enemyAnswer)
            {
                while (recognising && !battleManager.timesUp)
                {
                    for (int i = 0; i < battleManager.CurrentAnswers.Count; i++)
                    {
                        RecogniseOption(battleManager.CurrentAnswers[i], i);
                        if (KanamojiAnswerVerdict[i]) trueAnswerCount++;
                        LogOption(i);

                        yield return new WaitForSeconds(timeToDecideAnswer);

                        if (AIgotAnswerEarly || battleManager.timesUp)
                        {
                            recognising = false;
                            break;
                        }
                    }
                    if (trueAnswerCount >= 1) recognising = false;
                    yield return null;
                }

                if (trueAnswerCount > 1) KeepBestKnownAnswer();

                if (battleManager.timesUp)
                {
                    enemyAIState = EnemyAIState.TOMEMORIZELATER; // Too slow.
                    done = true;
                }
                else if (!recognising)
                {
                    enemyAIState = EnemyAIState.ANSWERQUESTION;
                    done = true;
                }
            }
            yield return null;
        }
        evaluating = false;
    }

    // Skill questions come in a timed series, and the AI speeds up as the timer and questions run down.
    IEnumerator EvaluateSkillAnswersCO()
    {
        evaluating = true;
        ResetAnswerState();
        bool recognising = true;
        float answerSpeed = 1f;
        yield return new WaitForSeconds(1f);

        bool done = false;
        while (!done)
        {
            if (battleManager.skillTimesUp)
            {
                enemyAIState = EnemyAIState.TOMEMORIZELATER; // Too slow.
                done = true;
            }
            else if (!battleManager.skillAnswer)
            {
                while (recognising)
                {
                    for (int i = 0; i < battleManager.CurrentAnswers.Count; i++)
                    {
                        answerSpeed = AIanswerSpeedModifier(answerSpeed);
                        RecogniseOption(battleManager.CurrentAnswers[i], i);
                        if (KanamojiAnswerVerdict[i]) trueAnswerCount++;

                        yield return new WaitForSeconds(Mathf.Max(timeToDecideAnswer / answerSpeed, 0.1f));

                        if (AIgotAnswerEarly)
                        {
                            recognising = false;
                            break;
                        }
                    }
                    if (trueAnswerCount >= 1) recognising = false;
                    yield return null;
                }

                if (trueAnswerCount > 1) KeepBestKnownAnswer();

                if (!battleManager.timesUp && !recognising)
                {
                    enemyAIState = EnemyAIState.ANSWERQUESTION;
                    done = true;
                }
            }
            yield return null;
        }
        evaluating = false;
    }

    // Answers speed up as the skill timer runs low and as fewer questions remain.
    public float AIanswerSpeedModifier(float answerSpeed)
    {
        float halfTime = battleManager.skillTime / 2;
        if (battleManager.currentSkillTimer < 1) answerSpeed += 1f;
        else if (battleManager.currentSkillTimer < halfTime) answerSpeed += 0.7f;

        if (battleManager.SklQuestionCount == 2) answerSpeed += 1f;
        else if (battleManager.SklQuestionCount == 1) answerSpeed += 1.5f;

        return answerSpeed;
    }

    public void AIAnswerSkillManager()
    {
        battleManager.startEnemyReactTimer = false;
        battleManager.ReactTimesUp = false;
        enemyAIState = EnemyAIState.READY;
    }

    // Submit the chosen answer and move on depending on whether it was right.
    void AnswerQuestion()
    {
        if (battleManager.battleSkill)
        {
            battleManager.OnAnswerButton();
            AIAnswerSkillManager();
            bool correct = battleManager.skillAnswer && battleManager.skillAnswerCorrect;
            enemyAIState = correct ? EnemyAIState.READY : EnemyAIState.TOMEMORIZELATER;
        }
        else
        {
            bool correct = AIchosenAnswer == questionIndex;
            battleManager.enemyAnswer = true;
            battleManager.enemyAnswerCorrect = correct;
            enemyAIState = correct ? EnemyAIState.IDLE : EnemyAIState.TOMEMORIZELATER;
            if (logAIDecisions)
                Debug.Log((correct ? "Enemy Answered Correctly: " : "Enemy Answered Wrong: ")
                          + persistData.HiraganaCharacters[AIchosenAnswer]);
        }

        if (logAIDecisions)
            Debug.Log("Time for enemy to answer: " + totalTimeToAnswer);
    }
}
