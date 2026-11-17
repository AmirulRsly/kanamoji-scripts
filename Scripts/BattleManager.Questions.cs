using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Building kana questions and running the attack and skill question rounds.
public partial class BattleManager
{
    // ---------------------------------------------------------------- Questions

    // The kana for a character index, in the script the player is learning (0 = hiragana, 1 = katakana).
    string Kana(int index)
    {
        return persistData.KanamojiCode == 0 ? persistData.HiraganaCharacters[index] : persistData.KatakanaCharacters[index];
    }

    // An answer list of the given size, all zeros, ready to be filled.
    void ResetAnswers(int count)
    {
        CurrentAnswers.Clear();
        for (int i = 0; i < count; i++) CurrentAnswers.Add(0);
    }

    // Fill the answers with wrong options from `source` (skipping the correct one), then put the correct
    // answer in slot 0. The caller shuffles afterwards.
    void FillAnswers(List<int> source, int sourceCount)
    {
        int j = 0;
        for (int i = 0; i < sourceCount && j < CurrentAnswers.Count; i++)
        {
            if (source[i] == CorrectAnswerIndex) continue;
            CurrentAnswers[j++] = source[i];
        }
        CurrentAnswers[0] = CorrectAnswerIndex;
    }

    // Put the answers on the first `count` buttons, as kana or as romaji.
    void LabelButtons(List<GameObject> buttons, int count, bool asKana, System.Func<TMP_FontAsset> font)
    {
        for (int i = 0; i < count; i++)
        {
            TextMeshProUGUI label = buttons[i].GetComponentInChildren<TextMeshProUGUI>();
            TMP_FontAsset f = font();
            if (f != null) label.font = f;
            label.text = asKana ? Kana(CurrentAnswers[i]) : persistData.RomajiCharacters[CurrentAnswers[i]];
        }
    }

    TMP_FontAsset RandomFont() { return DefaultFont[Random.Range(0, DefaultFont.Count)]; }

    /// <summary>
    /// Builds the next question and its answer options.
    /// Attack: on the player's turn the question is a kana and the answers are romaji;
    /// on the enemy's turn it is the other way round.
    /// Skill: three rounds that get harder: 2 romaji answers, then 4 kana, then 8 kana.
    /// </summary>
    void RandomizeQuestion(string QuestionType)
    {
        int lessonSize = currentKanamojiLesson.Count;

        if (QuestionType == "Attack")
        {
            // 3-kana lessons (yoon) get 3 answers; 5+ kana lessons get 4.
            ResetAnswers(lessonSize == 3 ? 3 : lessonSize >= 5 ? 4 : 0);
            int shownButtons = lessonSize == 3 ? answerBTNAtk.Count - 1 : lessonSize >= 5 ? answerBTNAtk.Count : 0;

            // KanamojiAnswerSequence is pre-shuffled, so questions don't repeat.
            CorrectAnswerIndex = KanamojiAnswerSequence[0];

            if (lessonSize == 3)
            {
                // Every kana in the lesson is an option, including the correct one.
                for (int i = 0; i < lessonSize; i++) CurrentAnswers[i] = currentKanamojiLesson[i];
            }
            else if (lessonSize >= 5)
            {
                FillAnswers(currentKanamojiLesson, 5);
            }
            Shuffler.Shuffle(CurrentAnswers);

            if (playerTurn)
            {
                AtkQuestionText.font = RandomFont();
                AtkQuestionText.text = Kana(CorrectAnswerIndex);
                LabelButtons(answerBTNAtk, shownButtons, false, () => null);
            }
            else
            {
                AtkQuestionText.text = persistData.RomajiCharacters[CorrectAnswerIndex];
                TMP_FontAsset font = RandomFont(); // One font for all of the enemy's options.
                LabelButtons(answerBTNAtk, shownButtons, true, () => font);
            }
        }
        else if (QuestionType == "Skill")
        {
            if (SklQuestionCount == 3)
            {
                // Round 1: the kana, pick the romaji from 2.
                ResetAnswers(2);
                CorrectAnswerIndex = Random.Range(enemyAIBrain.KanamojiFirstIndex, enemyAIBrain.KanamojiLastIndex);
                SklQuestionText.font = RandomFont();
                SklQuestionText.text = Kana(CorrectAnswerIndex);

                SkillFirstQuestion = GetUniqueRandomElements(currentKanamojiLesson, lessonSize);
                SkillFirstQuestion.Remove(CorrectAnswerIndex);
                for (int i = 0; i < 2; i++) CurrentAnswers[i] = SkillFirstQuestion[i];
                CurrentAnswers[0] = CorrectAnswerIndex;
                Shuffler.Shuffle(CurrentAnswers);

                LabelButtons(answerBTNSkl, 2, false, () => DefaultFont[0]);
            }

            if (SklQuestionCount == 2)
            {
                // Round 2: the romaji, pick the kana from 4.
                ResetAnswers(4);
                CorrectAnswerIndex = Random.Range(enemyAIBrain.KanamojiFirstIndex, enemyAIBrain.KanamojiLastIndex);
                SklQuestionText.font = DefaultFont[0];
                SklQuestionText.text = persistData.RomajiCharacters[CorrectAnswerIndex];

                if (lessonSize == 3) // yoon lessons draw wrong answers from all the yoon
                {
                    SkillSecondYoonQuestion = GetUniqueRandomElements(yoonList, 8);
                    FillAnswers(SkillSecondYoonQuestion, 4);
                }
                else if (lessonSize == 5 || lessonSize == 6)
                {
                    FillAnswers(currentKanamojiLesson, 5);
                }
                CurrentAnswers[0] = CorrectAnswerIndex;
                Shuffler.Shuffle(CurrentAnswers);

                LabelButtons(answerBTNSkl, 4, true, RandomFont);
            }

            if (SklQuestionCount == 1)
            {
                // Round 3: the romaji, pick the kana from 8, with wrong answers from beyond the lesson.
                ResetAnswers(8);
                CorrectAnswerIndex = Random.Range(enemyAIBrain.KanamojiFirstIndex, enemyAIBrain.KanamojiLastIndex);
                SklQuestionText.font = DefaultFont[0];
                SklQuestionText.text = persistData.RomajiCharacters[CorrectAnswerIndex];

                if (lessonSize == 3) // yoon
                {
                    FillAnswers(GetUniqueRandomElements(yoonList, 8), 8);
                }
                else
                {
                    // Eight random kana from the whole script, as character indices.
                    List<string> script = persistData.KanamojiCode == 0 ? persistData.HiraganaCharacters : persistData.KatakanaCharacters;
                    List<int> picks = GetUniqueRandomElements(script, 8).ConvertAll(k => script.IndexOf(k));
                    FillAnswers(picks, 8);
                }
                Shuffler.Shuffle(CurrentAnswers);

                LabelButtons(answerBTNSkl, answerBTNSkl.Count, true, RandomFont);
            }
        }
    }

    void ShowReactTimer(bool activate)
    {
        if(activate)
        reactTimerGO.LeanScaleX(1,0.2f).setEaseOutBack();
        else
        reactTimerGO.LeanScaleX(0, 0.2f).setEaseInCirc();
    }

    void SpeedTimer(bool activate)
    {
        if(activate)
        {
            startTimerPlayer = true;
        }
        else
        {
            currentPlayerTimer = 0;
            startTimerPlayer = false;
        }
    }

    // How many attack answer buttons this lesson uses: 3 for a 3-kana lesson, otherwise 4.
    int AttackButtonCount()
    {
        if (currentKanamojiLesson.Count == 3) return 3;
        if (currentKanamojiLesson.Count >= 4) return 4;
        return 0;
    }

    // Flip the attack answer buttons up into view one by one, or drop them away.
    IEnumerator AnswerButtonAnimAtkCO(bool activate)
    {
        int count = AttackButtonCount();
        for (int i = 0; i < count; i++)
        {
            Transform button = answerBTNAtk[i].transform;
            if (activate)
            {
                button.localRotation = Quaternion.Euler(0, -180, 0);
                yield return new WaitForSeconds(0.1f);
                button.LeanMoveLocalY(-42, 0.3f).setEaseOutBack();
                button.LeanRotateY(0, 0.3f).setEaseOutCirc();
            }
            else
            {
                yield return new WaitForSeconds(0.05f);
                button.LeanMoveLocalY(-191, 0.1f).setEaseInBack();
                button.LeanRotateY(-180, 0.3f).setEaseInCirc();
            }
        }
    }

    // Lock or unlock the attack answer buttons.
    void SetAttackButtonsInteractable(bool interactable, int count)
    {
        for (int i = 0; i < count; i++)
            answerBTNAtk[i].GetComponent<Button>().interactable = interactable;
    }

    // The attacker's instruction: read the kana and pick its romaji, or the other way round.
    string AttackInstruction()
    {
        string script = persistData.KanamojiCode == 0 ? "Hiragana" : "Katakana";
        return playerTurn
            ? "Click on the correct romaji\nbased on the " + script + " above\nbefore the enemy does"
            : "Click on the correct " + script + "\nbased on the romaji above\nbefore the enemy does";
    }

    // Letterbox the screen once someone has answered.
    void BlackBarsIn()
    {
        blurLerp = true;
        BlackBarBottom.LeanMoveLocalY(-10.74f, blackborderInAnimSpeed).setEaseOutCubic();
        BlackBarTop.LeanMoveLocalY(13.52f, blackborderInAnimSpeed).setEaseOutCubic();
    }

    // Flip a skill answer button over to show a tick (child 1) or a cross (child 2).
    IEnumerator AnswerButtonAnimSklCO(bool answerTrue, GameObject answerButtonSkl)
    {
        answerButtonSkl.transform.LeanRotateY(180, 0.3f).setEaseInCirc();
        yield return new WaitForSeconds(0.3f);
        answerButtonSkl.transform.GetChild(answerTrue ? 1 : 2).gameObject.SetActive(true);
        yield return new WaitForSeconds(1f);
    }

    IEnumerator AtkQuestionCO()
    {
        // Only one battle coroutine runs at a time.
        if(coroutineStarted)
        {
            yield break;
        }
        coroutineStarted = true;

        playerAnswer = false;
        playerAnswerCorrect = false;
        enemyAnswer = false;
        enemyAnswerCorrect = false;
        playerAnim.SetBool("isReady",true);
        enemyAnim.SetBool("isReady",true);
        PlayerUI(false);

            SetAttackButtonsInteractable(true, answerBTNAtk.Count);

            if(playerTurn)
            {
                if(battleAttack && !battleSkill)
                {
                    BattleLogText.text = "The Player Attacks!";
                    BattleLogTrans.LeanMoveLocalX(0f,0.2f).setEaseOutBack();
                    yield return new WaitForSeconds (1f);
                    BattleLogTrans.LeanMoveLocalX(760f,0.5f).setEaseInBack();
                    yield return new WaitForSeconds (0.5f);
                }

            }

            ProceedPrompt = true;
            string instruction = AttackInstruction();
            instructionTextEXE(true, instruction);
            while(!proceed)
            {
                yield return null;
            }
            proceed = false;
            ProceedPrompt = false;
            instructionTextEXE(false, instruction);

            yield return new WaitForSeconds (1f);

        questionIsGiven  = true;
        audioSource.PlayOneShot(QuestionSFX,0.5f);
        QuestionGOTrans.LeanMoveLocalY(153.2f, 0.2f).setEaseOutBack();
        yield return new WaitForSeconds (1f);

        AnswerButtonEXE(true);
        answersOptionShown = true;
        currentTime = 99;
        startTimer = true;
        SpeedTimer(true);
        bool blackborder = false;
        bool answered = false;
        bool SFXplayed = false;
        bool SFXplayedEnemy = false;
        float playerTimeToAnswer = 0;

        while(!timesUp)
        {
            if(playerAnswer)
            {
                //This whole block used to run every frame for the rest of the question.
                //LeanMove queues a NEW tween on each call rather than replacing the
                //running one, so the reaction window filled the LeanTween pool with
                //duplicates of the same dash. The move and the animator bool only need
                //firing once, so they go inside the existing one-shot guard.
                if(playerTurn)
                {
                    if(!SFXplayed)
                    {
                        playerTimeToAnswer = currentPlayerTimer;
                        SpeedTimer(false);
                        audioSource.PlayOneShot(StepSFX);
                        StartCoroutine(CoPlayDelayedClip(ForwardSFX,0.1f));

                        if(!playerStats.AttackRange)
                        playerGO.LeanMove(new Vector3(4.9f,-0.670000017f,-6.25f),animDuration1).setEaseOutQuart();
                        playerAnim.SetBool("isDashing",true);
                        SFXplayed = true;
                    }
                }
                else
                {
                    if(!SFXplayed)
                    {
                        playerTimeToAnswer = currentPlayerTimer;
                        SpeedTimer(false);
                        audioSource.PlayOneShot(GuardSFX);
                        playerAnim.SetBool("isDefending",true);
                        SFXplayed = true;
                    }
                }

                if(!blackborder)
                {
                    BlackBarsIn();
                    blackborder = true;
                }

                if(!answered)
                {
                    currentTimeToReact = reactionTime;
                    ShowReactTimer(true);
                    startTimeToReact = true;
                    playerAnswerFirst = true;
                    int buttons = AttackButtonCount();
                    for(int i = 0 ; i < buttons ; i++)
                        answerBTNAtk[i].GetComponent<AnswerButtonAnim>().resetAnsPos();
                    SetAttackButtonsInteractable(false, buttons);

                    audioSource.PlayOneShot(AnswerClickedSFX,0.3f);
                    currentEnemyReactTimer = enemyAIBrain.AitimetoReact;
                    playerStats.powerGuard = true;
                    currentTime = reactionTime;
                    answered = true;
                }

            }

            if(enemyAnswer)
            {
                //Same per-frame problem as the player branch above. The bare
                //audioSource.Play() also restarted the source every frame, cutting off
                //whatever PlayOneShot had just queued, so it goes with them.
                if(!playerTurn)
                {
                    if(!SFXplayedEnemy)
                    {
                        audioSource.PlayOneShot(StepSFX);
                        StartCoroutine(CoPlayDelayedClip(ForwardSFX,0.1f));

                        if(!enemyStats.AttackRange)
                        enemyGO.LeanMove(new Vector3(-6.9f,-0.810000002f,-6.25f),animDuration1).setEaseOutCubic();
                        enemyAnim.SetBool("isDashing",true);
                        SFXplayedEnemy = true;
                    }
                }
                else
                {
                    if(!SFXplayedEnemy)
                    {
                        audioSource.PlayOneShot(GuardSFX);
                        enemyAnim.SetBool("isDefending",true);
                        SFXplayedEnemy = true;
                    }
                }

                if(!blackborder)
                {
                    BlackBarsIn();
                    blackborder = true;
                }

                if(!answered)
                {
                    currentTimeToReact = reactionTime;
                    ShowReactTimer(true);
                    startTimeToReact = true;
                    currentEnemyReactTimer = playerAIBrain.AitimetoReact;
                    currentTime = reactionTime;
                    enemyStats.powerGuard = true;
                    answered = true;
                }
            }

            if(playerAnswer && enemyAnswer && !timesUp)
            {
                timesUp = true;
                startTimerEnemy = false;
                startTimeToReact = false;

            }
                yield return null;
        }
        ShowReactTimer(false);
        blackborder = false;
        QuestionGOTrans.LeanMoveLocalY(335, leanScaleAnim).setEaseInBack();
        AnswerButtonEXE(false);
        questionIsGiven = false;
        answersOptionShown = false;
        CalculatePerformance(CorrectAnswerIndex,playerTimeToAnswer,playerAnswer,playerAnswerCorrect);
        battleState = BattleState.ACTION;

        coroutineStarted = false;

    }

    // ---------------------------------------------------------------- Skill question helpers

    // Clear the right/wrong marks and rotation left on the skill answer buttons by the last round.
    void ResetSkillButtons()
    {
        for (int i = 0; i < answerBTNSkl.Count; i++)
        {
            answerBTNSkl[i].transform.localRotation = Quaternion.Euler(0, 0, 0);
            answerBTNSkl[i].transform.GetChild(1).gameObject.SetActive(false);
            answerBTNSkl[i].transform.GetChild(2).gameObject.SetActive(false);
        }
    }

    // Fan the first `count` skill answer buttons out to their places, or pull them back in.
    void ShowSkillButtons(int count)
    {
        for (int i = 0; i < count; i++)
            answerBTNSkl[i].transform.LeanMoveLocal(skillAnswerBTNPos[i], 0.2f);
    }

    void HideSkillButtons(int count)
    {
        for (int i = 0; i < count; i++)
            answerBTNSkl[i].transform.LeanMoveLocal(Vector3.zero, 0.2f).setEaseInBack();
    }

    // Right answer: the skill's power rises to `performance` (50, 100 or 150%). Wrong: a buzz.
    void PlaySkillResult(int performance)
    {
        if (skillAnswerCorrect)
        {
            audioSource.PlayOneShot(skillCorrectAnsSFX, 0.5f);
            SkillPerformanceValue = performance;
            countUpNum.CountNow(Mathf.RoundToInt(SkillPerformanceValue));
        }
        else
        {
            audioSource.PlayOneShot(skillWrongAnsSFX);
        }
    }

    // The skill rounds are over. A damage skill goes on to an attack question; any other skill resolves now.
    void EndSkillQuestions(skillID skill)
    {
        if (skill.skillType == 0)
        {
            RestoreStageAfterSkill();
            battleAttack = true;
            battleState = BattleState.QUESTION;
        }
        else
        {
            battleState = BattleState.PASSIVESKILL;
        }
    }

    // Bring the caster back from the skill close-up and un-blur the other battler.
    void RestoreStageAfterSkill()
    {
        if (playerTurn)
        {
            KnowledgeEnemyTrans.LeanScale(new Vector3(0.8f, 0.8f, 0.8f), 0.2f).setEaseOutBounce();
            enemyBarTrans.LeanMoveLocalX(10f, 0.2f).setEaseInOutBack();
            playerGO.LeanScale(playerScaleInit, 0.1f).setEaseOutCirc();
            playerGO.LeanMove(playerPosInit, 0.1f).setEaseOutCirc();
            unblurLerp = true;
            blurLayerSwap(enemyGO, false);
            enemySR.sortingLayerName = "Battlers";
            enemySR.sortingOrder = 0;
        }
        else
        {
            playerBarTrans.LeanMoveLocalX(560f, 0.5f).setEaseInOutBack();
            enemyGO.LeanScale(enemyScaleInit, 0.1f).setEaseOutCirc();
            enemyGO.LeanMove(enemyPosInit, 0.1f).setEaseOutCirc();
            unblurLerp = true;
            blurLayerSwap(playerGO, false);
            playerSR.sortingLayerName = "Battlers";
            playerSR.sortingOrder = 0;
        }
    }

    IEnumerator SklQuestionCO()
    {
        skillID skillid = skillSystem.SkillIGO.GetComponent<skillID>();
        float playerTimeToAnswer = 0;
        // Only one battle coroutine runs at a time.
        if(coroutineStarted)
        {
            yield break;
        }
        coroutineStarted = true;

        if(playerTurn)//Player
        {
            enemyBarTrans.LeanMoveLocalX( -366f, 0.2f).setEaseInOutBack();
            KnowledgeEnemyTrans.LeanScale(Vector3.zero,0.2f).setEaseInBounce();
            ProceedPrompt = true;
            instructionTextEXE(true,"Click on the matching answer \nbased on the Kanamoji given");
            while(!proceed)
            {
                yield return null;
            }
            proceed = false;
            ProceedPrompt = false;
            instructionTextEXE(false,"Click on the matching answer \nbased on the Kanamoji given");
            yield return new WaitForSeconds (1f);

            //------------
            skillPerformanceGOTrans.localPosition = new Vector3(377,0,0);
            yield return new WaitForSeconds(0.1f);
            AnswerGOTransSkl.localPosition = new Vector3(-153.199997f,-17.5f,0);
            ResetSkillButtons();
            //Change to charging skill animation
            activateInteractSkillBTN(true);
            PlayerUI(false);
            skillPerformanceGOTrans.LeanMoveLocalX(0,0.3f).setEaseInCirc();
            //First Skill Question
            RandomizeQuestion("Skill");
            yield return new WaitForSeconds(0.5f);

            startSkillTimer = true;
            SpeedTimer(true);
            activateInteractSkillBTN(true);
            audioSource.PlayOneShot(QuestionSFX,0.5f);
            AnswerGOTransSkl.LeanScale(Vector3.one,0.2f).setEaseOutBack();
            questionIsGiven = true;
            yield return new WaitForSeconds(0.5f);
            ShowSkillButtons(2);
            yield return new WaitForSeconds(0.2f);

            answersOptionShown = true;
            while(!skillAnswer)
            {
                if(skillTimesUp){skillAnswer = true; activateInteractSkillBTN(false);}
                yield return null;
            }
            playerTimeToAnswer = currentPlayerTimer;
            SpeedTimer(false);
            CalculatePerformance(CorrectAnswerIndex,playerTimeToAnswer,playerAnswerSkill,skillAnswerCorrect);

            questionIsGiven = false;
            answersOptionShown = false;
            pauseSkillTime = true;

            yield return new WaitForSeconds (0.3f);
            PlaySkillResult(50);
            if(skillAnswer)yield return new WaitForSeconds (0.7f);
            AnswerGOTransSkl.LeanScale(Vector3.zero,0.2f).setEaseInBack();
            //IF FIRST ANSWER IS WRONG ANSWER
                if(!skillAnswerCorrect || !skillAnswer)
                {
                    HideSkillButtons(2);
                    skillPerformanceGOTrans.LeanMoveLocalX(377,0.3f).setEaseOutCirc();
                    yield return new WaitForSeconds (0.7f);
                    audioSource.PlayOneShot(_0percentSkillSFX,0.5f);
                    BattleLogText.text = "The Player fails to activate the skill !";
                    BattleLogTrans.LeanMoveLocalX(0f,0.2f).setEaseOutBack();
                    yield return new WaitForSeconds (2f);
                    BattleLogTrans.LeanMoveLocalX(760f,0.5f).setEaseInBack();
                    ShowTurnText(false,playerTurn);
                    yield return new WaitForSeconds (0.5f);
                    playerGO.LeanScale(playerScaleInit, 0.1f).setEaseOutCirc();
                    playerGO.LeanMove(playerPosInit,0.1f).setEaseOutCirc();
                    unblurLerp = true;
                    blurLayerSwap(enemyGO,false);
                    enemySR.sortingLayerName = "Battlers";
                    enemySR.sortingOrder = 0;
                    playerTurn = false;
                    KnowledgeEnemyTrans.LeanScale(new Vector3(0.8f,0.8f,0.8f),0.2f).setEaseOutBounce();
                    enemyBarTrans.LeanMoveLocalX( 10f, 0.2f).setEaseInOutBack();
                    battleState = BattleState.TURN;
                    coroutineStarted = false;
                    yield break;
                }
            skillAnswerCorrect = false;
            playerAnswerSkill = false;
            HideSkillButtons(2);

            yield return new WaitForSeconds(0.5f);

            ResetSkillButtons();
            //Second Skill Question
            RandomizeQuestion("Skill");
            skillAnswer = false;

            yield return new WaitForSeconds(0.5f);
            audioSource.PlayOneShot(QuestionSFX,0.5f);
            AnswerGOTransSkl.LeanScale(Vector3.one,0.2f).setEaseOutBack();
            questionIsGiven = true;
            yield return new WaitForSeconds(0.5f);
            ShowSkillButtons(4);

            yield return new WaitForSeconds(0.2f);
            answersOptionShown = true;
            pauseSkillTime = false;
            SpeedTimer(true);
            activateInteractSkillBTN(true);

            while(!skillAnswer)
            {
                if(skillTimesUp){skillAnswer = true; activateInteractSkillBTN(false);}
                yield return null;
            }
            playerTimeToAnswer = currentPlayerTimer;
            SpeedTimer(false);
            CalculatePerformance(CorrectAnswerIndex,playerTimeToAnswer,playerAnswerSkill,skillAnswerCorrect);

            questionIsGiven = false;
            answersOptionShown = false;
            pauseSkillTime = true;
            yield return new WaitForSeconds (0.3f);
            PlaySkillResult(100);
            if(skillAnswer)yield return new WaitForSeconds (0.7f);

            AnswerGOTransSkl.LeanScale(Vector3.zero,0.2f).setEaseInBack();
            //IF SECOND ANSWER IS WRONG ANSWER
                if(!skillAnswerCorrect)
                {

                    RandomizeQuestion("Attack");

                    HideSkillButtons(4);
                    yield return new WaitForSeconds (1f);
                    skillPerformanceGOTrans.LeanMoveLocalX(377,0.3f).setEaseOutCirc();

                    yield return new WaitForSeconds (0.5f);
                    EndSkillQuestions(skillid);
                    coroutineStarted = false;
                    yield break;
                }
            skillAnswerCorrect = false;
            playerAnswerSkill = false;
            HideSkillButtons(4);

            yield return new WaitForSeconds(0.5f);

            ResetSkillButtons();

            //Third Skill Question
            RandomizeQuestion("Skill");
            skillAnswer = false;

            yield return new WaitForSeconds(0.5f);
            AnswerGOTransSkl.LeanScale(Vector3.one,0.2f).setEaseOutBack();
            questionIsGiven = true;
            audioSource.PlayOneShot(QuestionSFX,0.5f);
            yield return new WaitForSeconds(0.5f);
            ShowSkillButtons(8);

            yield return new WaitForSeconds(0.2f);
            answersOptionShown = true;
            pauseSkillTime = false;
            SpeedTimer(true);
            activateInteractSkillBTN(true);

            while(!skillAnswer)
            {
                if(skillTimesUp){skillAnswer = true; activateInteractSkillBTN(false);}
                yield return null;
            }
            playerTimeToAnswer = currentPlayerTimer;
            SpeedTimer(false);
            CalculatePerformance(CorrectAnswerIndex,playerTimeToAnswer,playerAnswerSkill,skillAnswerCorrect);

            questionIsGiven = false;
            answersOptionShown = false;
            pauseSkillTime = true;
            yield return new WaitForSeconds (0.3f);
            PlaySkillResult(150);
            if(skillAnswer)yield return new WaitForSeconds (0.7f);
            //IF THIRD ANSWER IS WRONG ANSWER
                if(!skillAnswerCorrect)
                {
                    RandomizeQuestion("Attack");
                    AnswerGOTransSkl.LeanScale(Vector3.zero,0.2f).setEaseInBack();

                    HideSkillButtons(8);
                    yield return new WaitForSeconds (1f);
                    skillPerformanceGOTrans.LeanMoveLocalX(377,0.3f).setEaseOutCirc();

                    yield return new WaitForSeconds (0.5f);
                    EndSkillQuestions(skillid);
                    coroutineStarted = false;
                    yield break;
                }
            skillAnswerCorrect = false;
            playerAnswerSkill = false;
            HideSkillButtons(8);
            yield return new WaitForSeconds(0.5f);

            AnswerGOTransSkl.LeanScale(Vector3.zero,0.2f).setEaseInBack();
            ResetSkillButtons();
            yield return new WaitForSeconds (1f);
            skillPerformanceGOTrans.LeanMoveLocalX(377,0.3f).setEaseOutCirc();

            //IF ALL 3 ANSWERS CORRECT
            RandomizeQuestion("Attack");

            yield return new WaitForSeconds (0.5f);

            skillAnswer = false;
            EndSkillQuestions(skillid);
        }

        else //Enemy turn
        {
            skillPerformanceGOTrans.localPosition = new Vector3(-874,0,0);
            activateInteractSkillBTN(false);
            yield return new WaitForSeconds(0.1f);
            AnswerGOTransSkl.localPosition = new Vector3(153.199997f,-17.5f,0);
            ResetSkillButtons();
            //Change to charging skill animation
            skillPerformanceGOTrans.LeanMoveLocalX(-589,0.3f).setEaseInCirc();
            //First Skill Question
            RandomizeQuestion("Skill");
            playerBarTrans.LeanMoveLocalX( 933f, 0.2f).setEaseInOutBack();
            yield return new WaitForSeconds(0.5f);

            startSkillTimer = true;
            audioSource.PlayOneShot(QuestionSFX,0.5f);
            AnswerGOTransSkl.LeanScale(Vector3.one,0.2f).setEaseOutBack();
            questionIsGiven = true;
            yield return new WaitForSeconds(0.5f);
            ShowSkillButtons(2);
            yield return new WaitForSeconds(0.2f);
            answersOptionShown = true;

            while(!skillAnswer)
            {
                if(skillTimesUp){skillAnswer = true; activateInteractSkillBTN(false);}
                yield return null;
            }
            questionIsGiven = false;
            answersOptionShown = false;
            pauseSkillTime = true;

            yield return new WaitForSeconds (0.3f);
            PlaySkillResult(50);
            if(skillAnswer)yield return new WaitForSeconds (0.7f);
            AnswerGOTransSkl.LeanScale(Vector3.zero,0.2f).setEaseInBack();
            //IF FIRST ANSWER IS WRONG ANSWER
                if(!skillAnswerCorrect || !skillAnswer)
                {
                    HideSkillButtons(2);
                    skillPerformanceGOTrans.LeanMoveLocalX(-874f,0.3f).setEaseOutCirc();
                    yield return new WaitForSeconds (0.7f);
                    audioSource.PlayOneShot(_0percentSkillSFX,0.5f);
                    BattleLogText.text = "The Enemy fails to activate the skill !";
                    BattleLogTrans.LeanMoveLocalX(0f,0.2f).setEaseOutBack();
                    yield return new WaitForSeconds (2f);
                    BattleLogTrans.LeanMoveLocalX(760f,0.5f).setEaseInBack();
                    ShowTurnText(false,playerTurn);
                    yield return new WaitForSeconds (0.5f);
                    enemyGO.LeanScale(enemyScaleInit, 0.1f).setEaseOutCirc();
                    enemyGO.LeanMove(enemyPosInit,0.1f).setEaseOutCirc();
                    unblurLerp = true;
                    blurLayerSwap(playerGO,false);
                    playerSR.sortingLayerName = "Battlers";
                    playerSR.sortingOrder = 0;
                    playerTurn = true;
                    playerBarTrans.LeanMoveLocalX( 560f, 0.5f).setEaseInOutBack();
                    battleState = BattleState.TURN;
                    coroutineStarted = false;
                    yield break;
                }
            skillAnswerCorrect = false;

            HideSkillButtons(2);

            yield return new WaitForSeconds(0.5f);

            ResetSkillButtons();
            //Second Skill Question
            RandomizeQuestion("Skill");
            skillAnswer = false;

            yield return new WaitForSeconds(0.5f);
            audioSource.PlayOneShot(QuestionSFX,0.5f);
            AnswerGOTransSkl.LeanScale(Vector3.one,0.2f).setEaseOutBack();
            questionIsGiven = true;
            yield return new WaitForSeconds(0.5f);
            ShowSkillButtons(4);

            yield return new WaitForSeconds(0.2f);
            answersOptionShown = true;
            pauseSkillTime = false;
            while(!skillAnswer)
            {
                if(skillTimesUp){skillAnswer = true; activateInteractSkillBTN(false);}
                yield return null;
            }
            questionIsGiven = false;
            answersOptionShown = false;
            pauseSkillTime = true;
            yield return new WaitForSeconds (0.3f);
            PlaySkillResult(100);
            if(skillAnswer)yield return new WaitForSeconds (0.7f);

            AnswerGOTransSkl.LeanScale(Vector3.zero,0.2f).setEaseInBack();
            //IF SECOND ANSWER IS WRONG ANSWER
                if(!skillAnswerCorrect)
                {
                    RandomizeQuestion("Attack");

                    HideSkillButtons(4);
                    yield return new WaitForSeconds (1f);
                    skillPerformanceGOTrans.LeanMoveLocalX(-874f,0.3f).setEaseOutCirc();

                    yield return new WaitForSeconds (0.5f);
                    EndSkillQuestions(skillid);
                    coroutineStarted = false;
                    yield break;
                }
            skillAnswerCorrect = false;

            HideSkillButtons(4);

            yield return new WaitForSeconds(0.5f);

            ResetSkillButtons();

            //Third Skill Question
            RandomizeQuestion("Skill");
            skillAnswer = false;

            yield return new WaitForSeconds(0.5f);
            AnswerGOTransSkl.LeanScale(Vector3.one,0.2f).setEaseOutBack();
            audioSource.PlayOneShot(QuestionSFX,0.5f);
            questionIsGiven = true;
            yield return new WaitForSeconds(0.5f);
            ShowSkillButtons(8);

            yield return new WaitForSeconds(0.2f);
            answersOptionShown = true;
            pauseSkillTime = false;
            while(!skillAnswer)
            {
                if(skillTimesUp){skillAnswer = true; activateInteractSkillBTN(false);}
                yield return null;
            }

            questionIsGiven = false;
            answersOptionShown = false;
            pauseSkillTime = true;
            yield return new WaitForSeconds (0.3f);
            PlaySkillResult(150);
            if(skillAnswer)yield return new WaitForSeconds (0.7f);
            //IF THIRD ANSWER IS WRONG ANSWER
                if(!skillAnswerCorrect)
                {
                    RandomizeQuestion("Attack");
                    AnswerGOTransSkl.LeanScale(Vector3.zero,0.2f).setEaseInBack();

                    HideSkillButtons(8);
                    yield return new WaitForSeconds (1f);
                    skillPerformanceGOTrans.LeanMoveLocalX(-874f,0.3f).setEaseOutCirc();

                    yield return new WaitForSeconds (0.5f);
                    EndSkillQuestions(skillid);
                    coroutineStarted = false;
                    yield break;
                }
            skillAnswerCorrect = false;

            HideSkillButtons(8);
            yield return new WaitForSeconds(0.5f);

            AnswerGOTransSkl.LeanScale(Vector3.zero,0.2f).setEaseInBack();
            ResetSkillButtons();
            yield return new WaitForSeconds (1f);
            skillPerformanceGOTrans.LeanMoveLocalX(-874f,0.3f).setEaseOutCirc();

            //IF ALL 3 ANSWERS CORRECT
            RandomizeQuestion("Attack");

            yield return new WaitForSeconds (0.5f);

            skillAnswer = false;
            EndSkillQuestions(skillid);
        }

        coroutineStarted = false;

    }
}
