using System;
using System.Collections.Generic;
using UnityEngine;

/*  ProjectName :FrameLoop
 *  ClassName   :PlayerInfo
 *  Creator     :Fujishita.Arashi
 *  
 *  Summary     :Playerの情報を統括するクラス
 *               
 *  Created     :2024/04/27
 */
public class PlayerInfo : SingletonMonoBehaviour<PlayerInfo>
{
    [SerializeField,Tooltip("プレイヤーが着地できるレイヤー")]
    LayerMask _platformLayer;

    [HideInInspector]
    public Rigidbody2D g_rb = null;
    [HideInInspector]
    public BoxCollider2D g_collider = null;
    [HideInInspector]
    public bool g_isGround = true;
    [HideInInspector]
    public Transform g_transform = null;
    [HideInInspector]
    public bool g_takeUpFg = false;
    [HideInInspector]
    public float g_wall = 0;
    [HideInInspector]
    public Transform g_box = null;
    [HideInInspector]
    public float g_groundDistance = 0;
    [HideInInspector]
    public bool g_isCrouch = false;

    private const float Ground_Dist = 0.8f;
    private string _layermaskValue;
    [HideInInspector]
    public LayerMask g_insideMask, g_outsideMask;

    private List<Transform> _copyList = new List<Transform>();

    private void Start()
    {
        g_rb = GetComponent<Rigidbody2D>();
        g_collider = GetComponent<BoxCollider2D>();
        g_transform = transform;

        //床のレイヤーを二進数に変換
        _layermaskValue = Convert.ToString(_platformLayer.value,2);

        //足場のレイヤーを内側用と外側用に分ける
        for (int i=0,j=_layermaskValue.Length-1; i < _layermaskValue.Length; i++,j--)
        {
            if (_layermaskValue[i] == '1')
            {
                //文字列の先頭がIかどうかで判定
                if (LayerMask.LayerToName(j)[0] == 'I')
                {
                    g_insideMask |= 1 << j;
                }
                else
                {
                    g_outsideMask |= 1 << j;
                }
            }
        }
    }

    private void Update()
    {
        if (!g_takeUpFg)
        {
            g_wall = 0;
        }
        //Debug.Log(g_groundDistance);

        float actualDistance = Mathf.Infinity, work = Mathf.Infinity;

        actualDistance = checkGround();

        List<Transform> removeList = new List<Transform>();

        //コピーからも着地判定を取る
        foreach (var t in _copyList)
        {
            if (t == null)
            {
                removeList.Add(t);
                continue;
            }

            work = checkGround(t);
            if (actualDistance > work)
            {
                actualDistance = work;
            }
        }

        foreach(var t in removeList)
        {
            _copyList.Remove(t);
        }

        g_groundDistance = actualDistance;

        //Debug.Log($"{g_groundDistance}： {actualDistance}");

        if (g_groundDistance < Ground_Dist)
        {
            g_isGround = true;

            g_isGround &= g_rb.velocity.y <= 0.1f;
        }
        else
        {
            g_isGround = false;
        }
    }

    private float checkGround(Transform t)
    {
        var distance = Mathf.Infinity;

        Ray ray = new Ray(t.position, Vector3.down);
        var size = new Vector2(g_collider.size.x - 0.1f, 0.5f);
        RaycastHit2D hit;

        LayerMask mask = 0;

        //フレームが有効かどうかでLayerMaskとLayerを変更
        if (FrameLoop.Instance.g_isActive)
        {
            mask = g_insideMask;
            gameObject.layer = LayerMask.NameToLayer("IPlayer");
        }
        else
        {
            mask = g_outsideMask;
            gameObject.layer = LayerMask.NameToLayer("OPlayer");
        }

        //足元に設置判定
        hit = Physics2D.BoxCast(ray.origin, size, 0, ray.direction, 2, mask);
        if (hit.collider != null)
        {
            //Debug.Log($"{hit.distance}{hit.transform.name}");

            //地面との距離を更新
            distance = hit.distance;

        }

        return distance;
    }

    private float checkGround()
    {
        var distance = Mathf.Infinity;

        Ray ray = new Ray(g_transform.position, Vector3.down);
        var size = new Vector2(g_collider.size.x - 0.1f, 0.5f);
        RaycastHit2D hit;

        LayerMask mask = 0;

        //フレームが有効かどうかでLayerMaskとLayerを変更
        if (FrameLoop.Instance.g_isActive)
        {
            mask = g_insideMask;
            gameObject.layer = LayerMask.NameToLayer("IPlayer");
        }
        else
        {
            mask = g_outsideMask;
            gameObject.layer = LayerMask.NameToLayer("OPlayer");
        }

        //足元に設置判定
        hit = Physics2D.BoxCast(ray.origin, size, 0, ray.direction, 2, mask);
        if (hit.collider != null)
        {
            //Debug.Log($"{hit.distance}{hit.transform.name}");

            //地面との距離を更新
            distance = hit.distance;

        }

        return distance;
    }

    public void AddCopyList(Transform t)
    {
        if (!_copyList.Contains(t))
        {
            _copyList.Add(t);
        }
    }

    public void RemoveCopyList(Transform t)
    {
        if (_copyList.Contains(t))
        {
            _copyList.Remove(t);
        }
    }

    public void ClearCopyList()
    {
        _copyList.Clear();
    }
}
