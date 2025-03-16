using System.Collections;
using UnityEngine;

public class LSystemDisplay : MonoBehaviour
{
    [SerializeField]
    LSystem lSystem;
    string stringToDraw;

    [SerializeField]
    bool iterateInstantly;
    [SerializeField]
    bool loop;

    void Start()
    {
        lSystem.Init(this);

        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        bool isFirstLoop = true;
        while (loop || isFirstLoop)
        {
            stringToDraw = lSystem.axiom;
            if (!isFirstLoop) lSystem.Reset();

            yield return StartCoroutine(lSystem.DrawString(stringToDraw, !iterateInstantly));
            yield return new WaitForSeconds(lSystem.iterationTime);

            for (int i = 0; i < lSystem.iterationCount; i++)
            {
                bool willRender = !iterateInstantly || i == lSystem.iterationCount - 1;

                stringToDraw = lSystem.IterateString(stringToDraw);
                lSystem.Reset();

                yield return StartCoroutine(lSystem.DrawString(stringToDraw, willRender));
                if (willRender) yield return new WaitForSeconds(lSystem.iterationTime);
            }

            isFirstLoop = false;
        }
    }
}
