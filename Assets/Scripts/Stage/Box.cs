using System.Collections.Generic;
using UnityEngine;

/*  ProjectName :FrameLoop
 *  ClassName   :Box
 *  Creator     :Fujishita.Arashi
 *  
 *  Summary     :箱の挙動を管理するクラス
 *               破壊可能な床の破壊、プレイヤーに掴まれている時の移動
 *               
 *  Created     :2024/04/27
 */
public class Box : MonoBehaviour,IBox
{
    private float _height = 0f;
    private Transform _transform, _playerTransform;
    [SerializeField,Tooltip("箱の横幅")]
    private float _width = 1f;
    [SerializeField,Tooltip("破壊するのに必要な高さ")]
    private float _breakHeight = 5f;
    [SerializeField,Tag,Tooltip("破壊可能なTag")]
    private List<string> _tagList = new List<string>() { "Breakable"};

    private Rigidbody2D _rb = null;

    private void Start()
    {
        _transform = transform;
        _rb = GetComponent<Rigidbody2D>();

        //y軸以外の動きを制限
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
    }

    private void Update()
    {
        platformBreak();
    }

    private void FixedUpdate()
    {
        isHold();
    }

    //破壊可能な床を壊す
    private void platformBreak()
    {
        //最高点の座標を更新
        if (_height < _transform.position.y)
        {
            _height = _transform.position.y;
        }

        Ray ray = new Ray(_transform.position, Vector3.down);
        RaycastHit2D hit;
        Vector2 size = new Vector2(_width / 2, 0.5f);

        /*このままだとフレームの中にある時Breakableの床を壊せない
         * 
         * 要修正
         */
        LayerMask mask = 1 << LayerMask.NameToLayer("OPlatform") | 1 << LayerMask.NameToLayer("OBox");
        if(gameObject.layer == LayerMask.NameToLayer("IBox"))
        {
            mask = 1 << LayerMask.NameToLayer("IPlatform") | 1 << LayerMask.NameToLayer("IBox");
        }

        hit = Physics2D.BoxCast(ray.origin, size, 0, ray.direction, 1, mask);
        if (hit.collider != null)
        {
            if (hit.distance > 0.3f) { return; }
            if (_tagList.Contains(hit.transform.tag))
            {
                //最高点との差が一定以上なら破壊する
                //最高点のリセットは行わない
                if (_height - _transform.position.y >= _breakHeight)
                {
                    Destroy(hit.transform.gameObject);
                }
            }
            else
            {
                //地面に触れたら最高点をリセット
                _height = _transform.position.y;
            }
        }
    }

    //プレイヤーが箱を持っているときの処理
    private void isHold()
    {
        if(_playerTransform == null) { return; }

        var pos = _rb.position;
        var direction = ((Vector2)_playerTransform.position - pos).normalized;
        pos.x = _playerTransform.position.x;
        pos += (Vector2)_playerTransform.right * 1;

        //x座標をプレイヤーの座標から一定距離ずらした位置にする
        _rb.position = pos;

        Ray ray = new Ray(pos, direction);
        RaycastHit2D[] hits;
        Vector2 size = new Vector2(_width / 2, 0.5f);


        //自分と同じレイヤーのBoxが進行方向にあるかチェック
        LayerMask mask = 1 << gameObject.layer;
        hits = Physics2D.BoxCastAll(ray.origin, size, 0, ray.direction, 0.2f, mask);

        if(hits.Length > 0)
        {
            foreach(var hit in hits)
            {
                //自分を除外して、座標を移動させる
                if(hit.transform == _transform) { continue; }

                var rb = hit.transform.GetComponent<Rigidbody2D>();
                pos.x += _width;
                rb.position = pos;
            }
        }
    }

    //箱を移動させる基準のtransformを受け取る
    public void Hold(Transform t)
    {
        _playerTransform = t;
    }
}
