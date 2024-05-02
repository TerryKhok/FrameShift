using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

/*  ProjectName :FrameLoop
 *  ClassName   :FrameLoop
 *  Creator     :Fujishita.Arashi
 *  
 *  Summary     :フレームの挙動を制御する
 *               
 *  Created     :2024/04/27
 */
public class FrameLoop : SingletonMonoBehaviour<FrameLoop>,IParentOnTrigger
{
    [SerializeField,Tooltip("当たり判定を生成するために設置するTile")]
    private Tile _tile = null;
    [SerializeField,Tooltip("内側の当たり判定用のTilemap")]
    private Tilemap _insideTile = null;
    [SerializeField, Tooltip("外側の当たり判定用のTilemap")]
    private Tilemap _outsideTile = null;
    [SerializeField,Tooltip("Frameに適用するMaterial(Scriptから色が変更されるので専用のものにする)")]
    private Material _material = null;
    [SerializeField,Tooltip("FrameのSize")]
    private Vector2Int _size = Vector2Int.one;
    [SerializeField,Tooltip("プレイヤーの座標からY方向にどれだけずらすか")]
    private float _yOffset = 1f;
    [SerializeField, Tooltip("しゃがみ中にプレイヤーの座標からY方向にどれだけずらすか")]
    private float _yOffset_Crouching = -2f;
    [SerializeField,Tooltip("切り替え")]
    private bool _toggle = false;

    private List<Collider2D>
        _insiders = new List<Collider2D>(),                         //フレームの内側のオブジェクトのリスト
        _enterOutsiders = new List<Collider2D>();                   //フレームの外に出ようとしているオブジェクトのリスト

    private Dictionary<Collider2D, Vector2>
        _outsiders = new Dictionary<Collider2D, Vector2>(),         //フレームの外側のオブジェクトと入ってくる方向のリスト
        _exitInsiders = new Dictionary<Collider2D, Vector2>();      //フレームの中に入ろうとしているオブジェクトと入ってくる方向のリスト

    private List<Collider2D> 
        _insideColliderList = new List<Collider2D>(),               //内側に生成したコライダーのリスト
        _outsideColliderList = new List<Collider2D> ();             //外側に生成したコライダーのリスト

    private Dictionary<Collider2D, Transform>
        _outsideCopyDic = new Dictionary<Collider2D, Transform>();  //フレームの外側のオブジェクトのコピーのリスト
    private Dictionary<Collider2D, List<Transform>>
    _insideCopyDic = new Dictionary<Collider2D, List<Transform>>(); //フレームの内側のオブジェクトのコピーのリスト

    private List<Fan> _fanList = new List<Fan>();                   //Fanクラスを取得したリスト
    private List<Button> _buttonList = new List<Button>();          //Buttonクラスを取得したリスト

    private (float min, float max) 
        _loopRangeX = (0, 0), _loopRangeY = (0, 0);                 //フレームの端の座標

    private BoxCollider2D _boxCollider = null;
    private Transform _playerTrans = null, _transform = null;
    private SpriteMask _spriteMask = null;
    private CompositeCollider2D
        _insideTileCol = null, _outsideTileCol = null;              //当たり判定生成用のタイルマップのコライダー
    private bool _isCrouching = false;
    private InputManager _inputManager = null;
    private PlayerInfo _playerInfo = null;
    private Goal _goal = null;
    private GameObject _colliderPrefab = null;
    private Transform _topT = null, _bottomT = null, _rightT = null, _leftT = null;

    [System.NonSerialized]
    public bool g_isActive = false, g_usable = true;
    private bool _prevActive = false;

    private void Start()
    {
        _transform = transform;

        //SpriteMaskを取得して無効にしておく
        _spriteMask = GetComponent<SpriteMask>();
        _spriteMask.enabled = false;

        //BoxColliderを取得してサイズを確定させる
        _boxCollider = GetComponent<BoxCollider2D>();
        _boxCollider.size = new Vector3(_size.x - 0.2f, _size.y - 0.2f, 1);

        //Singletonのスクリプトを取得しておく
        _playerInfo = PlayerInfo.Instance;
        _goal = Goal.Instance;

        //PlayerのTransformを取得
        _playerTrans = _playerInfo.g_transform;

        //Tilemapのコライダーを取得する
        _insideTileCol = _insideTile.GetComponent<CompositeCollider2D>();
        _outsideTileCol = _outsideTile.GetComponent<CompositeCollider2D>();

        //TilemapRendererを無効にする
        TilemapRenderer insideRenderer = _insideTile.GetComponent<TilemapRenderer>();
        insideRenderer.enabled = false;
        TilemapRenderer outsideRenderer = _outsideTile.GetComponent<TilemapRenderer>();
        outsideRenderer.enabled = false;

        _material.color = new Color32(255, 255, 0, 100);

        //生成するプレハブをロードしておく
        _colliderPrefab = (GameObject)Resources.Load("Collider");

        //InputManagerクラスを取得
        var managerObj = GameObject.FindGameObjectWithTag("GameManager");
        _inputManager = managerObj.GetComponent<InputManager>();

        //Fanスクリプトをすべて取得
        var fanObjs = GameObject.FindGameObjectsWithTag("Fan");
        foreach (var fanObj in fanObjs)
        {
            _fanList.Add(fanObj.GetComponent<Fan>());
        }

        //Buttonスクリプトを取得
        var buttonObjs = GameObject.FindGameObjectsWithTag("Button");
        foreach (var buttonObj in buttonObjs)
        {
            _buttonList.Add(buttonObj.GetComponent<Button>());
        }

        //子オブジェクトを取得して上下左右を割り当てる
        var children = transform.GetComponentsInChildren<Transform>().ToList();
        children.Remove(transform);
        _topT = children[0];
        _bottomT = children[1];
        _rightT = children[2];
        _leftT = children[3];

        //子オブジェクトの座標を調整する
        _topT.localPosition = new Vector3(0, _size.y / 2);
        _bottomT.localPosition = new Vector3(0, -_size.y / 2);
        _rightT.localPosition = new Vector3(_size.x / 2, 0);
        _leftT.localPosition = new Vector3(-_size.x / 2, 0);

        //子オブジェクトのスケールを調整する
        _topT.localScale = new Vector3(_size.x + 0.2f, 0.2f, 1);
        _bottomT.localScale = new Vector3(_size.x + 0.2f, 0.2f, 1);
        _rightT.localScale = new Vector3(0.2f, _size.y, 1);
        _leftT.localScale = new Vector3(0.2f, _size.y, 1);

        //子オブジェクトの当たり判定を調整
        BoxCollider2D childCol = null;
        childCol = _topT.GetComponent<BoxCollider2D>();
        childCol.size = new Vector2(1f, 5f);
        childCol.offset = new Vector2(0, 2.5f);
        childCol = _bottomT.GetComponent<BoxCollider2D>();
        childCol.size = new Vector2(1f, 5f);
        childCol.offset = new Vector2(0, -2.5f);
        childCol = _rightT.GetComponent<BoxCollider2D>();
        childCol.size = new Vector2(5f, 1f + 2 / (float)_size.y);
        childCol.offset = new Vector2(2.5f, 0);
        childCol = _leftT.GetComponent<BoxCollider2D>();
        childCol.size = new Vector2(5f, 1f + 2 / (float)_size.y);
        childCol.offset = new Vector2(-2.5f, 0);
    }

    private void Update()
    {
        //使用可能かと合わせて状態を決定する
        g_isActive &= g_usable;

        if (g_isActive)
        {
            //オブジェクトのコピーを生成
            instantiateCopy();
        }

        //nullチェックをしてnullならリストから削除
        List<Collider2D> workList = new List<Collider2D>(_insiders);
        foreach(var col in workList)
        {
            if(col == null) 
            {
                _insiders.Remove(col);
                if (_exitInsiders.ContainsKey(col))
                {
                    _exitInsiders.Remove(col);
                }
                continue;
            }

            //ループするオブジェクトの落下速度を制限
            var rb = col.GetComponent<Rigidbody2D>();
            var velocity = rb.velocity;
            if(velocity.y < -15f)
            {
                velocity.y = -15f;
                rb.velocity = velocity;
            }
        }

        //nullチェックをしてnullならリストから削除
        Dictionary<Collider2D, Vector2> workDict = new Dictionary<Collider2D, Vector2>(_outsiders);
        foreach (var col in workDict.Keys)
        {
            if (col == null)
            {
                _outsiders.Remove(col);
                if (_exitInsiders.ContainsKey(col))
                {
                    _enterOutsiders.Remove(col);
                }
                continue;
            }
        }

        //着地したら使用可能に戻す
        g_usable |= PlayerInfo.instance.g_isGround;

        //フレームの座標を調節
        adjustPos();

        //フレームが有効になったとき行う処理
        if (!_prevActive && g_isActive)
        {
            onActive();
        }

        //フレームが無効になったとき行う処理
        if(_prevActive && !g_isActive)
        {
            onInactive();
        }
    }

    private void LateUpdate()
    {   
        //前フレームの状態を保存
        _prevActive = g_isActive;
    }

    //フレームが有効になった時に一度実行するメソッド
    private void onActive()
    {
        //フレームの端の座標を更新して確定
        _loopRangeX.min = _transform.position.x - (_size.x / 2);
        _loopRangeX.max = _transform.position.x + (_size.x / 2);
        _loopRangeY.min = _transform.position.y - (_size.y / 2);
        _loopRangeY.max = _transform.position.y + (_size.y / 2);

        //SpriteMaskを有効にする
        _spriteMask.enabled = true;
        _material.color = new Color32(0, 255, 0, 150);

        List<Collider2D> removeList = new List<Collider2D>();

        foreach (var col in _insiders)
        {
            //外側のオブジェクトのリストにも存在する場合、座標でどちらかに分類する
            if (_outsiders.ContainsKey(col))
            {
                var pos = col.transform.position;
                if (pos.x < _loopRangeX.min || pos.x > _loopRangeX.max ||
                    pos.y < _loopRangeY.min || pos.y > _loopRangeY.max)
                {
                    removeList.Add(col);
                    continue;
                }
            }

            //内側のオブジェクトをSpriteMaskの中でのみ表示されるよう変更
            SpriteRenderer renderer = col.GetComponent<SpriteRenderer>();
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            //Player以外の内側のオブジェクトはレイヤーをI~~~レイヤーに変更
            if (col.CompareTag("Player"))
            {
                continue;
            }
            col.gameObject.layer++;
        }

        //外側のリストに分類されたオブジェクトを内側のリストから削除
        foreach(var col in removeList)
        {
            _insiders.Remove(col);
            if (_exitInsiders.ContainsKey(col))
            {
                _exitInsiders.Remove(col);
            }
        }


        //フレームの範囲+-1マス分の範囲をループ
        for (int i=0; i <= _size.x+1; i++)
        {
            for(int j=0; j <= _size.y+1; j++)
            {

                //座標が角なら次のループへ
                if (i == 0 || i == _size.x + 1)
                {
                    if (j == 0 || j == _size.y + 1)
                    {
                        continue;
                    }
                }

                //Rayの原点をフレームの左下を基準に決定する
                var origin = new Vector2(_loopRangeX.min, _loopRangeY.min);
                origin.x += -0.5f + i;
                origin.y += -0.5f + j;

                RaycastHit2D[] hit;

                //Rayを作成
                var screenPos = Camera.main.WorldToScreenPoint(origin);
                Ray ray = Camera.main.ScreenPointToRay(screenPos);

                //LayerMaskを外側の当たり判定にのみ当たるようにする
                LayerMask layerMask = 0;
                layerMask |= 1 << LayerMask.NameToLayer("OPlatform");
                layerMask |= 1 << LayerMask.NameToLayer("OBox");

                hit = Physics2D.RaycastAll(ray.origin, ray.direction, 15, layerMask);

                //Rayが当たらなかったら次のループへ
                if (hit.Length == 0)
                {
                    continue;
                }
                else
                {
                    bool instantFg = false;
                    foreach(var item in hit)
                    {
                        if (!_insideColliderList.Contains(item.collider))
                        {
                            if (item.transform.CompareTag("Box"))
                            {

                                //フレームの端に箱があったら箱を複製する
                                if (i == 0 || i < _size.x + 1 || j == 0 || j < _size.y + 1)
                                {
                                    ColliderInstantiate(item.transform.position, i, j, item.transform);
                                }
                                continue;
                            }
                            
                            //箱以外に当たったらinstantFgをTrueに
                            instantFg = true;
                        }
                    }

                    //箱以外に当たっていたらTileをセットする
                    if (instantFg)
                    {
                        setColliderTile(origin, i, j);
                    }
                }
            }
        }

        //風をループさせる
        foreach(var fan in _fanList)
        {
            fan.FanLoopStarted();
        }

        //ゴールのレイヤーをプレイヤーが触れられるレイヤーに変更
        _goal.GoalLayerCheck();

        //ボタンのレイヤーをプレイヤーが触れられるレイヤーに変更
        foreach (var button in _buttonList)
        {
            button.ButtonLayerCheck();
        }
    }

    //Tileをセットする
    private void setColliderTile(Vector2 origin,int i, int j)
    {
        Vector3 pos = origin;

        //座標がフレームの内側ならその場にTileをセット
        if (1 <= i && i <= _size.x)
        {
            if (1 <= j && j <= _size.y)
            {
                Vector3Int intPos = new Vector3Int((int)(pos.x - 0.5f), (int)(pos.y - 0.5f));
                _insideTile.SetTile(intPos, _tile);
            }
        }

        //座標がフレームの端か外側なら座標をループさせる
        if (i <= 1) { pos.x += _size.x; }
        else if (i >= _size.x) { pos.x -= _size.x; }
        else if (j <= 1) { pos.y += _size.y; }
        else if (j >= _size.y) { pos.y -= _size.y; }

        //生成する座標がフレームの外側なら内側用の当たり判定を生成
        if (pos.x < _loopRangeX.min || _loopRangeX.max < pos.x||
            pos.y < _loopRangeY.min || _loopRangeY.max < pos.y)
        {
            //Vector3Int intPos = new Vector3Int((int)(pos.x-0.5f), (int)(pos.y-0.5f));
            //_insideTile.SetTile(intPos, _tile);
            intPos = new Vector3Int((int)(origin.x - 0.5f), (int)(origin.y - 0.5f));
            _insideTile.SetTile(intPos, _tile);
        }
        else
        {
            //生成する座標がフレームの内側なら外側用の当たり判定を生成
            Vector3Int intPos = new Vector3Int((int)(pos.x - 0.5f), (int)(pos.y - 0.5f));
            _outsideTile.SetTile(intPos, _tile);
        }

        //フレームの左右の端で、上下の端か外側なら上下にループさせて当たり判定を生成
        if (i == 1 || i == _size.x)
        {
            if (j <= 1 || j >= _size.y)
            {
                pos = origin;
                if (j <= 1) { pos.y += _size.y; }
                if (j >= _size.y) { pos.y -= _size.y; }
                Vector3Int intPos = new Vector3Int((int)(pos.x - 0.5f), (int)(pos.y - 0.5f));

                //上下の端の座標なら内側用の当たり判定を生成
                if(j == 1 || j == _size.y)
                {
                    _insideTile.SetTile(intPos, _tile);
                }
                else
                {
                    //外側なら外側用の当たり判定を生成
                    _outsideTile.SetTile(intPos, _tile);
                }
            }
        }
    }

    //ブロックの当たり判定を生成
    private void ColliderInstantiate(Vector3 pos,int i, int j, Transform parent)
    {
        //当たったオブジェクトががコピーオブジェクトなら生成をやめる
        foreach(var tList in _insideCopyDic.Values)
        {
            if (tList.Contains(parent))
            {
                return;
            }
        }

        //座標をループさせる
        if (i <= 1) { pos.x += _size.x; }
        else if (i >= _size.x) { pos.x -= _size.x; }
        else if (j <= 1) { pos.y += _size.y; }
        else if (j >= _size.y) { pos.y -= _size.y; }

        //当たり判定を箱の子オブジェクトとして生成
        var instance = Instantiate(_colliderPrefab, pos, Quaternion.identity, parent);
        var col = instance.GetComponent<Collider2D>();
        instance.layer = 11;
        _outsideColliderList.Add(col);
    }

    //フレームが無効になったときに一度実行するメソッド
    private void onInactive()
    {
        _material.color = new Color32(255, 255, 0, 100);

        //SpriteMaskを無効にする
        _spriteMask.enabled = false;

        //内側のオブジェクトをすべてチェック
        foreach(var col in _insiders)
        {
            //SpriteMaskの外側で表示されるように変更
            SpriteRenderer renderer = col.GetComponent<SpriteRenderer>();
            renderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

            //外に出ようとしているオブジェクトの位置を確定
            if (_exitInsiders.ContainsKey(col))
            {
                var pos = col.transform.position;
                if(pos.x < _loopRangeX.min)
                {
                    pos.x += _size.x;
                }
                else if(pos.x > _loopRangeX.max)
                {
                    pos.x -= _size.x;
                }

                if (pos.y < _loopRangeY.min)
                {
                    pos.y += _size.y;
                }
                else if (pos.y > _loopRangeY.max)
                {
                    pos.y -= _size.y;
                }
                col.transform.position = pos;
            }

            //Player以外のレイヤーをO~~~レイヤーに変更
            if (col.CompareTag("Player"))
            {
                continue;
            }
            col.gameObject.layer--;
        }

        //使用不可能にする
        g_usable = false;

        //Tilemapを全てクリアする
        _insideTile.ClearAllTiles();
        _outsideTile.ClearAllTiles();

        //生成したコライダーをDestroy
        for(int i=0;i < _insideColliderList.Count; i++)
        {
            if (_insideColliderList[i] == null) { continue; }
            Destroy(_insideColliderList[i].gameObject);
        }
        for (int i = 0; i < _outsideColliderList.Count; i++)
        {
            if (_outsideColliderList[i] == null) { continue; }
            Destroy(_outsideColliderList[i].gameObject);
        }

        //コピーしたオブジェクトをDestroy
        Dictionary<Collider2D, List<Transform>> workDic = new Dictionary<Collider2D, List<Transform>>(_insideCopyDic); 
        foreach(var col in workDic.Keys)
        {
            foreach(Transform t in _insideCopyDic[col])
            {
                if (t == null)
                {
                    continue;
                }
                Destroy(t.gameObject);
            }
        }

        //コピーしたオブジェクトをDestroy
        Dictionary<Collider2D, Transform> workDic2 = new Dictionary<Collider2D, Transform>(_outsideCopyDic);
        foreach (var col in workDic2.Keys)
        {
            if (_outsideCopyDic[col] == null)
            {
                continue;
            }
            Destroy(_outsideCopyDic[col].gameObject);
        }

        //リストをクリア
        _insideColliderList.Clear();
        _outsideColliderList.Clear();
        _insideCopyDic.Clear();
        _outsideCopyDic.Clear();
        _exitInsiders.Clear();
        _enterOutsiders.Clear();

        //風のループをやめる
        foreach (var fan in _fanList)
        {
            fan.FanLoopCanceled();
        }

        //ゴールのレイヤーをプレイヤーが触れられるレイヤーに変更
        _goal.SetOutsideLayer();

        //ボタンのレイヤーをプレイヤーが触れられるレイヤーに変更
        foreach (var button in _buttonList)
        {
            button.SetOutsideLayer();
        }
    }

    //キーが押された時に一度実行されるメソッド
    public void FrameStarted(InputAction.CallbackContext context)
    {
        //操作が切り替えなら押されるたびに状態を切り替え
        if (_toggle)
        {
            g_isActive = !g_isActive;
            if (g_isActive)
            {
                _inputManager.SetVibration(0.2f, 0f, 0f);
            }
            else
            {
                _inputManager.SetVibration(0f, 0f, 0f);
            }
            return;
        }

        //操作がホールドならフレームを有効にする
        g_isActive = true;
        _inputManager.SetVibration(0.2f, 0f, 0f);
    }

    //キーが離された時に一度実行されるメソッド
    public void FrameCanceled(InputAction.CallbackContext context)
    {
        //操作が切り替えならreturn
        if(_toggle) { return; }

        //操作がホールドならフレームを無効にする
        _inputManager.SetVibration(0, 0, 0);
        g_isActive = false;
    }

    //座標をプレイヤーの座標で調節
    private void adjustPos()
    {
        //フレームが有効なら座標を固定する
        if(g_isActive) { return; }

        //しゃがんでいるかでy座標を決定する
        var setPos = _playerTrans.position;
        if (_isCrouching)
        {
            setPos.y += _yOffset_Crouching;
        }
        else
        {
            setPos.y += _yOffset;
        }

        //座標を四捨五入して整数座標を求める
        setPos.x = (float)Math.Round(setPos.x, MidpointRounding.AwayFromZero);
        setPos.y = (float)Math.Round(setPos.y, MidpointRounding.AwayFromZero);
        _transform.position = setPos;
    }

    //フレームでループするオブジェクトをコピーする
    private void instantiateCopy()
    {
        //外側に出るオブジェクトを全て確認
        foreach (var col in _exitInsiders.Keys)
        {
            //コピーが無ければ複製する
            if (!_insideCopyDic.ContainsKey(col))
            {
                //外側に出るオブジェクトのコピーを取得
                GameObject obj = copy(col.transform);

                //座標をコピー元の座標と揃える
                var pos = col.transform.position;

                //座標をフレームの大きさ分ひたり下にずらす
                pos -= new Vector3(_size.x,_size.y);

                List<Transform> tList = new List<Transform>();
                for(int i = 0; i < 3; i++)
                {
                    for(int j = 0; j < 3; j++)
                    {
                        if(i == 1 &&  j == 1) { continue; }

                        //座標を左下からループして子オブジェクトとして生成
                        //上下左右に八個のコピーが生成される
                        Vector3 setPos = pos;
                        setPos += new Vector3(_size.x * i, _size.y * j);
                        var instanceObj = Instantiate(obj, setPos, col.transform.rotation, col.transform);
                        tList.Add(instanceObj.transform);
                    }
                }

                //コピーのリストをリストへまとめる
                _insideCopyDic.Add(col, tList);

                //コピー用のオブジェクトを削除する
                Destroy( obj );
            }
        }

        //内側に入るオブジェクトを全て確認
        foreach(var col in _enterOutsiders)
        {
            //コピーが無ければ複製する
            if (!_outsideCopyDic.ContainsKey(col))
            {
                //内側に入るオブジェクトのコピーを取得
                GameObject obj = copy(col.transform);

                //どこから入ってきているかを取得
                var vec = _outsiders[col];

                //入ってくるのと反対方向に座標をずらす
                var pos = col.transform.position;
                pos -= Vector3.Scale(vec, new Vector3(_size.x,_size.y));

                //子オブジェクトとして生成する
                var instanceObj = Instantiate(obj, pos, col.transform.rotation, col.transform);

                //リストに追加する
                _outsideCopyDic.Add(col, instanceObj.transform);

                //コピー用のオブジェクトを削除する
                Destroy(obj);
            }
        }
    }

    //オブジェクトをコピー
    private GameObject copy(Transform t)
    {
        GameObject obj = new GameObject(t.name + "_copy");
        obj.layer = t.gameObject.layer;

        //コンポーネントをコピー
        SpriteRenderer setRenderer = t.GetComponent<SpriteRenderer>();
        Rigidbody2D setRigidbody = t.GetComponent<Rigidbody2D>();

        obj.AddComponent<ParentDestroy>();

        //コピーしたコンポーネントをアタッチ
        obj.AddComponent(setRenderer);
        var rb = obj.AddComponent(setRigidbody);
        rb.isKinematic = true;
        rb.useAutoMass = false;

        if (t.CompareTag("Box"))
        {
            //箱を複製するときはBoxChildをアタッチ
            obj.AddComponent<BoxChild>();
        }

        //コライダーコンポーネントを取得
        var col = t.GetComponent<Collider2D>();


        //コライダーのタイプによってアタッチするコンポーネントのタイプを変換
        switch (col)
        {
            case BoxCollider2D:
                BoxCollider2D setBoxCol = col as BoxCollider2D;
                obj.AddComponent(setBoxCol);
                break;
            case CircleCollider2D:
                CircleCollider2D setCircleCol = col as CircleCollider2D;
                obj.AddComponent(setCircleCol);
                break;
            case CapsuleCollider2D:
                CapsuleCollider2D setCapsuleCol = col as CapsuleCollider2D;
                obj.AddComponent(setCapsuleCol);
                break;
        }

        //オブジェクトを返す
        return obj;
    }

    //フレームの内側に入った時のメソッド
    private void OnTriggerStay2D(Collider2D other)
    {
        //コピーオブジェクトならreturn
        foreach(var col in _insideCopyDic.Keys)
        {
            if (_insideCopyDic[col].Contains(other.transform))
            {
                return;
            }
        }
        if (_outsideCopyDic.ContainsValue(other.transform))
        {
            return;
        }

        //生成したブロックならreturn
        if(_insideColliderList.Contains(other) || _outsideColliderList.Contains(other))
        {
            return;
        }

        //外側のオブジェクトで、フレームが有効な時
        if (_outsiders.ContainsKey(other) && g_isActive)
        {
            //内側に入るオブジェクトのリストに追加
            if (!_enterOutsiders.Contains(other))
            {
                _enterOutsiders.Add(other);
            }
        }
        else
        {
            //内側のオブジェクトのリストに無くて、フレームが無効な時
            if (!_insiders.Contains(other) && !g_isActive)
            {
                //内側のオブジェクトのリストに追加
                _insiders.Add(other);
            }
        }
    }

    //フレームの内側から出た時のメソッド
    private void OnTriggerExit2D(Collider2D other)
    {
        //外側に出るオブジェクトのリストにあって、フレームが有効なら
        if (_exitInsiders.ContainsKey(other) && g_isActive)
        {
            //出ていく方向を取得
            Vector2 vec = _exitInsiders[other];

            //座標を取得
            Transform t = other.transform;
            var pos = t.position;

            //出ていく方向と反対の座標に移動
            vec *= _size;
            pos -= (Vector3)vec;
            t.position = pos;
        }

        if (_insiders.Contains(other))
        {
            if (!g_isActive)
            {
                //内側のオブジェクトのリストにあって、フレームが無効ならリストから削除
                _insiders.Remove(other);
                if (_exitInsiders.ContainsKey(other))
                {
                    _exitInsiders.Remove(other);
                }
            }
        }

        //外に出るオブジェクトならリストから削除
        if (_enterOutsiders.Contains(other))
        {
            _enterOutsiders.Remove(other);
        }
    }

    //フレームの周りの当たり判定に入ったときのメソッド
    public void OnStay(Collider2D other,Transform transform)
    {
        //コピーオブジェクトならreturn
        foreach (var col in _insideCopyDic.Keys)
        {
            if (_insideCopyDic[col].Contains(other.transform))
            {
                return;
            }
        }
        if (_outsideCopyDic.ContainsValue(other.transform))
        {
            return;
        }

        //生成したコライダーならreturn
        if(_insideColliderList.Contains(other) || _outsideColliderList.Contains(other))
        {
            return;
        }

        //どこから入ってきてるかを取得
        Vector2 vec = Vector2.zero;
        if (transform == _topT) { vec.y = 1; }
        if (transform == _bottomT) { vec.y = -1; }
        if (transform == _rightT) { vec.x = 1; }
        if (transform == _leftT) { vec.x = -1; }

        //内側のオブジェクトのリストにあって、フレームが有効なら
        if (_insiders.Contains(other) && g_isActive)
        {
            //出ていくオブジェクトのリストに追加する
            if (!_exitInsiders.ContainsKey(other))
            {
                _exitInsiders.Add(other, vec);
            }
            else
            {;
                _exitInsiders[other] = vec;
            }
        }
        else
        {
            //Playerならreturn
            if (other.CompareTag("Player")) { return; }

            //外側のオブジェクトのリストに無ければ追加する
            if(!_outsiders.ContainsKey(other))
            {
                _outsiders.Add(other, vec);
            }
            else
            {
                _outsiders[other] = vec;
            }
        }
    }

    //フレームの周りの当たり判定から出た時のメソッド
    public void OnExit(Collider2D other, Transform transform)
    {
        //内側に入るオブジェクトのリストにあって、フレームが有効なら
        if (_enterOutsiders.Contains(other) && g_isActive)
        {
            //入ってくる方向を取得
            Vector2 vec = _outsiders[other];

            //座標を取得
            Transform t = other.transform;
            var pos = t.position;

            //入ってくる方向と反対に座標を移動
            vec *= _size;
            pos -= (Vector3)vec;
            t.position = pos;

            //コピーオブジェクトを削除する
            Destroy(_outsideCopyDic[other].gameObject);
            _outsideCopyDic.Remove(other);
        }

        //外側のオブジェクトのリストにあったら削除する
        if (_outsiders.ContainsKey(other))
        {
            _outsiders.Remove(other);

            //内側に入るオブジェクトのリストにあったら削除する
            if (_enterOutsiders.Contains(other))
            {
                _enterOutsiders.Remove(other);
            }
        }

        //外側に出るオブジェクトのリストにあったら削除する
        if (_exitInsiders.ContainsKey(other))
        {
            _exitInsiders.Remove(other);
        }
    }

    public void OnEnter(Collider2D other, Transform transform)
    {
        //フレームの周りの当たり判定に入った瞬間に呼ぶメソッド
    }

    //プレイヤーがしゃがんでるかを代入
    public void SetCrouching(bool isCrouching)
    {
        _isCrouching = isCrouching;
    }

    //フレームのサイズをVector2Intで返す
    public Vector2Int GetSize()
    {
        return _size;
    }
}
