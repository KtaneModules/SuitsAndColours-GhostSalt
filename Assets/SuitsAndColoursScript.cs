using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class SuitsAndColoursScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMColorblindMode Colourblind;
    public KMSelectable[] Buttons;
    public Sprite[] SuitSprites;
    public Sprite[] ColourSprites;
    public SpriteRenderer[] SuitRends;
    public SpriteRenderer[] Flashers;
    public Material ScanLineMat;
    public MeshRenderer[] LEDs;
    public Material[] LEDMats;
    public SpriteRenderer[] LEDGlows;

    private class LOBoard
    {
        public List<bool> Board { get; private set; }

        public LOBoard(List<bool> board)
        {
            Board = board;
        }

        public LOBoard Copy()
        {
            return new LOBoard(Board.ToList());
        }

        public void PressCells(List<int> cells)
        {
            var affected = new List<int[]>()
            {
                new int[] { 0, 1, 3 },
                new int[] { 0, 1, 2, 4 },
                new int[] { 1, 2, 5 },
                new int[] { 0, 3, 4, 6 },
                new int[] { 1, 3, 4, 5, 7 },
                new int[] { 2, 4, 5, 8 },
                new int[] { 3, 6, 7 },
                new int[] { 4, 6, 7, 8 },
                new int[] { 5, 7, 8 }
            };

            for (int i = 0; i < cells.Count(); i++)
                foreach (var cell in affected[cells[i]])
                    Board[cell] = !Board[cell];
        }

        public override string ToString()
        {
            var boardText = Board.Select(x => x ? "X" : ".");
            return boardText.Take(3).Join("") + "\n" + boardText.Skip(3).Take(3).Join("") + "\n" + boardText.Skip(6).Take(3).Join("");
        }
    }

    private const float ScanLineSpeed = 1f;

    private Color[] PossibleColours = new Color[] { Color.red, new Color(1, 0.5f, 0), new Color(1, 1, 0), Color.green };
    private List<int> ChosenColours = new List<int>();
    private List<int> ChosenSuits = new List<int>();
    private List<int> Selections = new List<int>();
    private List<int> AToC = new List<int>();
    private List<int> BToC = new List<int>();
    private int CompletedBits = -1, Stage;
    private LOBoard BoardA, BoardB, BoardC;
    private float Offset;
    private bool CannotPress = true, IsColourblind, Solved, Suspended, TPColourblind;

    void Awake()
    {
        _moduleID = _moduleIdCounter++;

        PossibleColours = PossibleColours.Select(x => x * new Color(1, 1, 1, 0.5f)).ToArray();

        StartCoroutine(RunScanLines());

        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            Buttons[x].OnInteract += delegate { if (!CannotPress) ButtonPress(x); return false; };
            Flashers[x].color = Color.clear;
            SuitRends[x].color = Color.clear;
        }

        for (int i = 0; i < LEDs.Length; i++)
        {
            LEDs[i].material = LEDMats[0];
            LEDGlows[i].color = Color.clear;
        }

        Calculate();

        Module.OnActivate += delegate { InitAnimHandler(); };
    }

    // Use this for initialization
    void Start()
    {
        IsColourblind = Colourblind.ColorblindModeActive;
        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            Buttons[x].OnHighlight += delegate { ChangeSpriteType(x, IsColourblind); };
            Buttons[x].OnHighlightEnded += delegate { ChangeSpriteType(x, false); };
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    void Calculate()
    {
        var cCount = Rnd.Range(3, 6);
        BoardC = new LOBoard(Enumerable.Range(0, 9).Select(x => x < cCount).ToList().Shuffle());

        AToC = Enumerable.Range(0, 9).ToList().Shuffle().Take(Rnd.Range(3, 6)).ToList();
        AToC.Sort();
        BoardA = BoardC.Copy();
        BoardA.PressCells(AToC);

        var aToB = Enumerable.Range(0, 9).Where(x => BoardC.Board[x]).ToList();
        BoardB = BoardA.Copy();
        BoardB.PressCells(aToB);

        BToC = AToC.ToList();
        BToC.AddRange(aToB);
        BToC = BToC.Where(x => BToC.Count(y => x == y) == 1).ToList();
        BToC.Sort();

        if (BToC.Count() < 3 || BToC.Count() > 5 || AToC.All(x => BToC.Contains(x)))
            Calculate();
        else
        {
            var invertSuits = Enumerable.Range(0, 9).Select(x => Rnd.Range(0, 2) == 0).ToList();
            var invertColours = Enumerable.Range(0, 9).Select(x => Rnd.Range(0, 2) == 0).ToList();

            var adjacent = new List<int[]>()
            {
                new int[] { 1, 3 },
                new int[] { 0, 2, 4 },
                new int[] { 1, 5 },
                new int[] { 0, 4, 6 },
                new int[] { 1, 3, 5, 7 },
                new int[] { 2, 4, 8 },
                new int[] { 3, 7 },
                new int[] { 4, 6, 8 },
                new int[] { 5, 7 }
            };

            for (int i = 0; i < Buttons.Length; i++)
            {
                var isInvertedSuit = adjacent[i].Select(x => invertColours[x]).Count(x => x) % 2 == 1;
                var isInvertedColour = adjacent[i].Select(x => invertSuits[x]).Count(x => x) % 2 == 1;
                ChosenSuits.Add((isInvertedSuit ^ BoardA.Board[i]) ? (invertSuits[i] ? 3 : 1) : (invertSuits[i] ? 2 : 0));
                ChosenColours.Add((isInvertedColour ^ BoardB.Board[i]) ? (invertColours[i] ? 3 : 1) : (invertColours[i] ? 2 : 0));
            }

            Debug.LogFormat("[Suits and Colours #{0}] The suits present are, in reading order: {1}.", _moduleID, ChosenSuits.Select(x => new[] { "spades", "hearts", "clubs", "diamonds" }[x]).Join(", "));
            Debug.LogFormat("[Suits and Colours #{0}] The colours present are, in reading order: {1}.", _moduleID, ChosenColours.Select(x => new[] { "red", "orange", "yellow", "green" }[x]).Join(", "));
            Debug.LogFormat("[Suits and Colours #{0}] Board A:\n{1}", _moduleID, BoardA.ToString());
            Debug.LogFormat("[Suits and Colours #{0}] Board B:\n{1}", _moduleID, BoardB.ToString());
            Debug.LogFormat("[Suits and Colours #{0}] The set of presses between these two boards is, in reading order: {1}.", _moduleID, aToB.Select(x => x + 1).Join(", "));
            Debug.LogFormat("[Suits and Colours #{0}] Board C:\n{1}", _moduleID, BoardC.ToString());
            Debug.LogFormat("[Suits and Colours #{0}] The set of presses between A & C is, in reading order: {1}.", _moduleID, AToC.Select(x => x + 1).Join(", "));
            Debug.LogFormat("[Suits and Colours #{0}] The set of presses between B & C is, in reading order: {1}.", _moduleID, BToC.Select(x => x + 1).Join(", "));
        }
    }

    void ButtonPress(int pos)
    {
        if (Suspended)  // While suspended on Stage 2
        {
            Buttons[pos].AddInteractionPunch();
            Audio.PlaySoundAtTransform("press", Buttons[pos].transform);
            CannotPress = true;
            Suspended = false;
            for (int i = 0; i < 9; i++)
                StartCoroutine(HideSuit(i));
            Debug.LogFormat("[Suits and Colours #{0}] Exited suspension mode.", _moduleID);
        }
        else if (!Selections.Contains(pos))
        {
            Buttons[pos].AddInteractionPunch();
            Audio.PlaySoundAtTransform("press", Buttons[pos].transform);    // Will be played no matter what. Not the case for "set", which plays only on correct presses.

            if (Stage == 0)
            {
                if (BoardC.Board[pos])  // Stage 1, correct cell
                {
                    Selections.Add(pos);
                    StartCoroutine(SetCell(pos));
                    Audio.PlaySoundAtTransform("set", Buttons[pos].transform);

                    if (Selections.Count() == BoardC.Board.Where(x => x).Count())   // Stage 1, all correct cells
                    {
                        Stage++;
                        CannotPress = true;
                        Selections = new List<int>();
                        LEDs[0].material = LEDMats[1];
                        LEDGlows[0].color = new Color(1, 1, 1, 0.5f);
                        StartCoroutine(Progress());
                        Debug.LogFormat("[Suits and Colours #{0}] You pressed cell {1}, which was correct. Onto Stage 2!", _moduleID, pos + 1);
                    }
                    else
                        Debug.LogFormat("[Suits and Colours #{0}] You pressed cell {1}, which was correct.", _moduleID, pos + 1);
                }
                else    // Stage 1, incorrect cell. In this case, some of the board may have been hidden, so the board and input need to be reset.
                {
                    Module.HandleStrike();
                    Selections = new List<int>();
                    for (int i = 0; i < 9; i++)
                    {
                        SuitRends[i].sprite = SuitSprites[ChosenSuits[i]];
                        SuitRends[i].color = PossibleColours[ChosenColours[i]];
                    }
                    Debug.LogFormat("[Suits and Colours #{0}] You pressed cell {1}, which was incorrect. Strike! (Input has been reset, too.)", _moduleID, pos + 1);
                }
            }
            else
            {
                if ((AToC.Contains(pos) && Selections.All(x => AToC.Contains(x))) || (BToC.Contains(pos) && Selections.All(x => BToC.Contains(x))))     // Stage 2, new press creates valid set of presses for either A & C or B & C
                {
                    Selections.Add(pos);
                    StartCoroutine(SetCell(pos));
                    Audio.PlaySoundAtTransform("set", Buttons[pos].transform);

                    var completedZone = AToC.All(x => Selections.Contains(x)) ? 0
                        : BToC.All(x => Selections.Contains(x)) ? 1
                        : -1;
                    if (completedZone > -1)     // If a zone was just completed
                    {
                        if (completedZone != CompletedBits)     // If the completed zone has not been done already
                        {
                            CompletedBits = completedZone;
                            CannotPress = true;
                            Selections = new List<int>();
                            LEDs[Stage].material = LEDMats[1];
                            LEDGlows[Stage].color = new Color(1, 1, 1, 0.5f);
                            Stage++;
                            if (Stage == 3)     // If both zones have been completed
                            {
                                Solved = true;
                                Module.HandlePass();
                                Audio.PlaySoundAtTransform("solve", transform);
                                StartCoroutine(SolveAnim());
                                Debug.LogFormat("[Suits and Colours #{0}] You pressed cell {1}. That's both sets complete. Module solved!", _moduleID, pos + 1);
                            }
                            else    // If just one zone was completed
                            {
                                StartCoroutine(Progress());
                                Debug.LogFormat("[Suits and Colours #{0}] You pressed cell {1}. You formed set {2}! Now, you need to form set {3}.", _moduleID, pos + 1, new[] { "A&C", "B&C" }[completedZone], new[] { "B&C", "A&C" }[completedZone]);
                            }
                        }
                        else    // If the completed zone has already been done
                        {
                            CannotPress = true;
                            Selections = new List<int>();
                            StartCoroutine(DoneAlready());
                            Debug.LogFormat("[Suits and Colours #{0}] You pressed cell {1}. You've already formed set {2}. I expected set {3}, but that's okay.", _moduleID, pos + 1, new[] { "A&C", "B&C" }[completedZone], new[] { "B&C", "A&C" }[completedZone]);
                        }
                    }
                    else
                        Debug.LogFormat("[Suits and Colours #{0}] You pressed cell {1}. So far so good.", _moduleID, pos + 1);
                }
                else    // Stage 2, new press creates a set of presses within neither A & C or B & C. In this case, I think a half reset is best, hence suspension mode. The input for Stage 1 is not required, a single press works fine. Input should be reset.
                {
                    Module.HandleStrike();
                    CannotPress = true;
                    Suspended = true;
                    Selections = new List<int>();

                    for (int i = 0; i < 9; i++)
                        StartCoroutine(InitAnim(i, 0.5f));

                    Debug.LogFormat("[Suits and Colours #{0}] You pressed cell {1}, which I didn't expect. Strike! (And suspension mode has been enabled, too.)", _moduleID, pos + 1);
                }
            }
        }
    }

    void ChangeSpriteType(int pos, bool isColourblind)
    {
        if (SuitRends[pos].sprite == null || SuitRends[pos].sprite.name == "cross")
            return;
        if (isColourblind)
            SuitRends[pos].sprite = ColourSprites[ChosenColours[pos]];
        else
            SuitRends[pos].sprite = SuitSprites[ChosenSuits[pos]];
    }

    private IEnumerator Progress()
    {
        float timer = 0;
        while (timer < 0.1f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        Audio.PlaySoundAtTransform("correct", transform);
        for (int i = 0; i < 9; i++)
            StartCoroutine(HideSuit(i));
    }

    private IEnumerator DoneAlready(float sustain = 0.125f, float pause = 0.125f)
    {
        var inits = new List<Color>();
        for (int i = 0; i < 9; i++)
            inits.Add(SuitRends[i].color);
        float timer = 0;
        while (timer < 0.1f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        for (int i = 0; i < 3; i++)
        {
            Audio.PlaySoundAtTransform("done already", transform);
            for (int j = 0; j < 9; j++)
                SuitRends[j].color = inits[j] * new Color(1, 0, 0, 1);
            timer = 0;
            while (timer < sustain)
            {
                yield return null;
                timer += Time.deltaTime;
            }
            for (int j = 0; j < 9; j++)
                SuitRends[j].color = Color.clear;
            timer = 0;
            while (timer < pause)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
        CannotPress = false;
    }

    private IEnumerator RunScanLines()
    {
        while (true)
        {
            yield return null;
            Offset += Time.deltaTime;
            Offset %= 1;
            ScanLineMat.SetTextureOffset("_MainTex", new Vector2(0, Offset + (Time.deltaTime * ScanLineSpeed)));
        }
    }

    private IEnumerator SetCell(int pos)
    {
        Flashers[pos].color = new Color(1, 1, 1, 0.5f);
        SuitRends[pos].sprite = SuitSprites[4];
        SuitRends[pos].color = new Color(1, 1, 1, 0.5f);
        float timer = 0;
        while (timer < 0.05f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        Flashers[pos].color = Color.clear;
    }

    private IEnumerator SolveAnim(float interval = 0.075f, float ledPause = 1, float ledInterval = 0.125f)
    {
        float timer = 0;
        var order = new int[][] { new int[] { 0 }, new int[] { 1, 3 }, new int[] { 2, 4, 6 }, new int[] { 5, 7 }, new int[] { 8 } };
        for (int i = 0; i < order.Length; i++)
        {
            for (int j = 0; j < order[i].Length; j++)
                StartCoroutine(CellFade(order[i][j]));
            timer = 0;
            while (timer < interval)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }

        timer = 0;
        while (timer < ledPause)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        for (int i = 0; i < LEDs.Length; i++)
        {
            LEDs[i].material = LEDMats[0];
            LEDGlows[i].color = Color.clear;
        }
        timer = 0;
        while (timer < ledInterval)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        for (int i = 0; i < LEDs.Length; i++)
        {
            LEDs[i].material = LEDMats[1];
            LEDGlows[i].color = new Color(1, 1, 1, 0.5f);
        }
        timer = 0;
        while (timer < ledInterval)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        for (int i = 0; i < LEDs.Length; i++)
        {
            LEDs[i].material = LEDMats[0];
            LEDGlows[i].color = Color.clear;
        }
    }

    private IEnumerator CellFade(int pos, float duration = 0.3f)
    {
        Flashers[pos].color = Color.clear;
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Flashers[pos].color = new Color(1, 1, 1, Easing.InSine(timer, 0, 1, duration));
        }
        Flashers[pos].color = Color.white;
        SuitRends[pos].sprite = null;
        timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            var value = Easing.OutSine(timer, 1, 0, duration);
            Flashers[pos].color = new Color(value, value, value, 1);
        }
        Flashers[pos].color = Color.black;
    }

    void InitAnimHandler()
    {
        for (int i = 0; i < 9; i++)
            StartCoroutine(InitAnim(i));
    }

    private IEnumerator InitAnim(int pos, float duration = 1f, float variance = 0.4f)
    {
        SuitRends[pos].sprite = SuitSprites[ChosenSuits[pos]];

        float timer = 0;
        while (timer < duration)
        {
            SuitRends[pos].color = PossibleColours[ChosenColours[pos]] * new Color(1, 1, 1, Mathf.Clamp((timer / duration) + Rnd.Range(-variance, variance), 0, 1));
            yield return null;
            timer += Time.deltaTime;
        }

        SuitRends[pos].color = PossibleColours[ChosenColours[pos]];
        CannotPress = false;
    }

    private IEnumerator HideSuit(int pos, float duration = 0.5f, float variance = 0.4f)
    {
        var init = SuitRends[pos].color;
        float timer = 0;
        while (timer < duration)
        {
            SuitRends[pos].color = init * new Color(1, 1, 1, Mathf.Clamp(1 - (timer / duration) + Rnd.Range(-variance, variance), 0, 1));
            yield return null;
            timer += Time.deltaTime;
        }

        SuitRends[pos].color = Color.clear;
        CannotPress = false;
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} 123' to press the three cells on the top row — coordinates are labelled 1-9 in reading order. Use '!{0} colo(u)rblind' to toggle colour abbreviations — these will disappear when cells are pressed.";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (command.ToLowerInvariant() == "colourblind" || command.ToLowerInvariant() == "colorblind")
        {
            yield return null;
            TPColourblind = !TPColourblind;
            for (int j = 0; j < 9; j++)
                ChangeSpriteType(j, TPColourblind);
            yield break;
        }
        var validCmds = "123456789";
        for (int i = 0; i < command.Length; i++)
        {
            if (!validCmds.Contains(command[i]))
            {
                yield return "sendtochaterror Invalid command.";
                yield break;
            }
        }
        yield return null;
        for (int i = 0; i < command.Length; i++)
        {
            TPColourblind = false;
            for (int j = 0; j < 9; j++)
                ChangeSpriteType(j, false);
            while (CannotPress)
                yield return null;
            Buttons[validCmds.IndexOf(command[i])].OnInteract();
            float timer = 0;
            while (timer < 0.1f)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        while (!Solved)
        {
            while (CannotPress)
                yield return null;
            if (Suspended)
                Buttons[0].OnInteract();
            else
            {
                if (Stage == 0)
                {
                    var zeroPresses = Enumerable.Range(0, 9).Where(x => BoardC.Board[x] && !Selections.Contains(x)).ToList();
                    for (int i = 0; i < zeroPresses.Count(); i++)
                    {
                        Buttons[zeroPresses[i]].OnInteract();
                        yield return null;
                    }
                }
                else if (Selections.Any(x => !BToC.Contains(x)) // If the current input doesn't align with B,
                    || (Selections.All(x => AToC.Contains(x)) && Selections.All(x => BToC.Contains(x)) && CompletedBits != 0))  // Or it could be either input and A hasn't been done yet.
                {
                    var aPresses = AToC.Where(x => !Selections.Contains(x)).ToList();
                    for (int i = 0; i < aPresses.Count(); i++)
                    {
                        Buttons[aPresses[i]].OnInteract();
                        yield return null;
                    }
                }
                else
                {
                    var bPresses = BToC.Where(x => !Selections.Contains(x)).ToList();
                    for (int i = 0; i < bPresses.Count(); i++)
                    {
                        Buttons[bPresses[i]].OnInteract();
                        yield return null;
                    }
                }
            }
        }
    }
}