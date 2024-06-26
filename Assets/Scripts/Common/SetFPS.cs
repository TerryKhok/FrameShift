using UnityEngine;

/*  ProjectName :FrameLoop
 *  ClassName   :SetFPS
 *  Creator     :Fujishita.Arashi
 *  
 *  Summary     :FPSを固定する
 *               
 *  Created     :2024/04/27
 */
public class SetFPS : MonoBehaviour
{
    [SerializeField, Tooltip("FPS上限")]
    private int _targetFPS = 120;
    private void Awake()
    {
        Application.targetFrameRate = _targetFPS;
    }
}
