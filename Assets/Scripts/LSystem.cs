using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CreateAssetMenu(menuName = "L-System")]
public class LSystem : ScriptableObject
{
    [SerializeField]
    Color background;
    [SerializeField]
    Color[] colors;
    [SerializeField]
    bool isGradient = false;
    [SerializeField]
    int gradientCount;
    ColorPicker colorPicker;
    public int colorIndex
    {
        get { return colorPicker.i; }
        set { colorPicker.i = value; }
    }

    [SerializeField]
    Material lineMaterial;
    [SerializeField]
    float lineWidthStart;
    [SerializeField]
    float lineWidthEnd;
    DrawTool drawTool;

    public LRule[] rules;

    public DrawInstruction[] drawInstructions;

    public int iterationCount;
    public float iterationTime;
    public float drawTime;

    [SerializeField]
    float turnAngle;
    float runtimeTurnAngle;
    float turnRadians { get { return 2f * Mathf.PI * runtimeTurnAngle / 360f; } }
    public float drawDistance;

    public string axiom;

    Vector2 turtlePosition = Vector2.zero;
    float turtleAngle = 0f;

    float savedAngle;

    public void Init(LSystemDisplay _display)
    {
        Camera.main.backgroundColor = background;

        colorPicker = new ColorPicker(isGradient, gradientCount, colors);
        drawTool = new DrawTool(Vector3.zero, lineMaterial, colorPicker.color, lineWidthStart, lineWidthEnd);

        runtimeTurnAngle = turnAngle;

        Reset();
    }

    [MenuItem("Assets/Duplicate L-System")]
    static void Duplicate()
    {
        LSystem duplicate = Instantiate(Selection.activeObject as LSystem);

        string duplicandPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        string localDuplicandName = Path.GetFileNameWithoutExtension(duplicandPath);
        string[] duplicandDirectoryPath = Path.GetDirectoryName(duplicandPath).Replace("\\", "/").Split("/".ToCharArray());
        string duplicandFolderName = duplicandDirectoryPath[duplicandDirectoryPath.Length - 1];
        UnityEngine.Object[] assets = Resources.LoadAll(duplicandFolderName, typeof(LSystem));
        int duplicateCount = 0;
        foreach (UnityEngine.Object candidate in assets) if (AssetIsDuplicate(candidate)) duplicateCount++;

        AssetDatabase.CreateAsset(duplicate, Path.ChangeExtension(duplicandPath, null) + string.Format("({0})", duplicateCount) + ".asset");
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();

        Selection.activeObject = duplicate;

        bool AssetIsDuplicate(UnityEngine.Object asset)
        {
            string localName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(asset));
            return localName.StartsWith(localDuplicandName + "(") || localName == localDuplicandName;
        }
    }
    [MenuItem("Assets/Duplicate L-System", true)]
    static bool AssetIsLSystem() { return Selection.activeObject is LSystem; }

    public void Reset()
    {
        drawTool.Reset();
        colorPicker.Reset();
        StartLine(Vector3.zero, lineMaterial, colorPicker.color);
    }

    public virtual string IterateString(string s)
    {
        char[] chars = s.ToCharArray();
        string output = "";
        for (int i = 0; i < chars.Length; i++)
        {
            bool ruleApplied = false;
            foreach (LRule rule in rules)
            {
                if (chars[i] == rule.input)
                {
                    output += rule.output;
                    ruleApplied = true;
                }
            }
            if (!ruleApplied)
                output += chars[i];
        }
        MonoBehaviour.print(output + "\nCharacter Count: " + output.Length + " Sign Count: " + output.Count((x) => x == '+' || x == '-'));
        return output;
    }

    void ResetTurtle()
    {
        turtlePosition = Vector2.zero;
        turtleAngle = 0f;
    }
    
    public virtual IEnumerator DrawString(string s, bool willRender)
    {
        ResetTurtle();

        List<Tuple<float, Vector2>> savedStates = new List<Tuple<float, Vector2>>();
        List<int> savedColors = new List<int>();

        char[] drawChars = s.ToCharArray();
        for (int i = 0; i < drawChars.Length; i++)
        {
            foreach(DrawInstruction drawInstruction in drawInstructions)
            {
                if (drawInstruction.input == drawChars[i])
                {
                    switch (drawInstruction.output)
                    {
                        case DrawInstruction.Instruction.Forward:
                            if (!willRender) break;
                            DrawForward();
                            if (drawTime > 0f) yield return new WaitForSeconds(drawTime);
                            break;

                        case DrawInstruction.Instruction.TurnLeft:
                            if (!willRender) break;
                            turtleAngle += turnRadians;
                            break;

                        case DrawInstruction.Instruction.TurnRight:
                            if (!willRender) break;
                            turtleAngle -= turnRadians;
                            break;

                        case DrawInstruction.Instruction.HalfAngle:
                            runtimeTurnAngle /= 2f;
                            break;

                        case DrawInstruction.Instruction.Save:
                            if (!willRender) break;
                            savedStates.Add(new Tuple<float, Vector2>(turtleAngle, turtlePosition));
                            savedColors.Add(colorIndex);
                            break;

                        case DrawInstruction.Instruction.Load:
                            if (!willRender) break;
                            Tuple<float, Vector2> state = savedStates[savedStates.Count - 1];
                            savedStates.RemoveAt(savedStates.Count - 1);
                            turtleAngle = state.Item1;
                            turtlePosition = state.Item2;

                            colorIndex = savedColors[savedColors.Count - 1];
                            savedColors.RemoveAt(savedColors.Count - 1);

                            Jump(turtlePosition);
                            break;

                        case DrawInstruction.Instruction.ChangeColor:
                            if (!willRender) break;
                            ChangeColor();
                            break;
                    }
                }
            }
        }
    }

    void DrawForward()
    {
        Vector2 target = turtlePosition + new Vector2(Mathf.Cos(turtleAngle) * drawDistance, Mathf.Sin(turtleAngle) * drawDistance);
        drawTool.DrawLine(target);
        turtlePosition = target;
    }

    void ChangeColor()
    {
        colorPicker.Iterate();
        drawTool.StartLine(lineMaterial, colorPicker.color);
    }

    void Jump(Vector2 target)
    {
        drawTool.StartLine(target, lineMaterial, colorPicker.color);
    }

    public void StartLine(Vector3 start, Material material, Color color) { drawTool.StartLine(start, material, color); }

    class DrawTool
    {
        List<GameObject> lines = new List<GameObject>();

        float lineWidthStart;
        float lineWidthEnd;

        public DrawTool(Vector3 start, Material material, Color color, float _lineWidthStart, float _lineWidthEnd)
        {
            lineWidthStart = _lineWidthStart;
            lineWidthEnd = _lineWidthEnd;
        }

        public void StartLine(Vector3 start, Material material, Color color)
        {
            GameObject line = new GameObject();
            LineRenderer lineRenderer = line.AddComponent<LineRenderer>();

            lineRenderer.material = material;
            lineRenderer.material.color = color;
            lineRenderer.startWidth = lineWidthStart;
            lineRenderer.endWidth = lineWidthEnd;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.useWorldSpace = false;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.numCapVertices = 5;
            lineRenderer.numCornerVertices = 5;

            lineRenderer.positionCount = 1;
            lineRenderer.SetPosition(0, start);

            lines.Add(line);
        }
        public void StartLine(Material material, Color color)
        {
            LineRenderer lineRenderer = lines[lines.Count - 1].GetComponent<LineRenderer>();
            StartLine(lineRenderer.GetPosition(lineRenderer.positionCount - 1), material, color);
        }

        public void DrawLine(Vector3 end)
        {
            LineRenderer lineRenderer = lines[lines.Count - 1].GetComponent<LineRenderer>();
            lineRenderer.positionCount++;
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, end);
        }

        public void Reset()
        {
            if (lines.Count == 0) return;
            foreach (GameObject line in lines) GameObject.Destroy(line);
            lines = new List<GameObject>();
        }
    }

    class ColorPicker
    {
        public Color color { get { return colors[i]; } }
        Color[] colors;
        public int i = 0;
        bool isGradient = false;

        public ColorPicker(bool _isGradient, int gradientCount, params Color[] _colors)
        {
            isGradient = _isGradient;
            if (!isGradient)
            {
                colors = _colors;
                return;
            }

            Color target = _colors[_colors.Length - 1];
            colors = new Color[gradientCount];
            float increment = 1f / (gradientCount - 1);
            for (int i = 0; i < gradientCount; i++)
                colors[i] = Color.Lerp(_colors[0], target, i * increment);
        }

        public void Iterate()
        {
            i++;
            if (i == colors.Length)
            {
                if (isGradient)
                {
                    i = colors.Length - 1;
                    return;
                }
                i = 0;
            }
        }

        public void Reset() { i = 0; }
    }
}
