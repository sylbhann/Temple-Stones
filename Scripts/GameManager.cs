using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour {
    [SerializeField] private int _width = 4;
    [SerializeField] private int _height = 4;
    [SerializeField] private Node _nodePrefab;
    [SerializeField] private Block _blockPrefab;
    [SerializeField] private SpriteRenderer _boardPrefab;
    [SerializeField] private List<BlockType> _types;
    [SerializeField] private float _travelTime = 0.2f;
    [SerializeField] private int _winCondition = 2048;
    [SerializeField] private GameObject _mergeEffectPrefab;
    [SerializeField] private FloatingText _floatingTextPrefab;

    [SerializeField] private GameObject _winScreen, _loseScreen,_winScreenText;
    [SerializeField] private AudioClip[] _moveClips;
    [SerializeField] private AudioClip[] _matchClips;
    [SerializeField] private AudioSource _source;

    private List<Node> _nodes;
    private List<Block> _blocks;
    private GameState _state;
    private int _round;

    private BlockType GetBlockTypeByValue(int value) => _types.First(t => t.Value == value);

    void Start() {
       ChangeState(GameState.GenerateLevel);
    }

    private void ChangeState(GameState newState) {
        _state = newState;

        switch (newState) {
            case GameState.GenerateLevel:
                GenerateGrid();
                break;
            case GameState.SpawningBlocks:
                SpawnBlocks(_round++ == 0 ? 2 : 1);
                break;
            case GameState.WaitingInput:
                break;
            case GameState.Moving:
                break;
            case GameState.Win:
                _winScreen.SetActive(true);
                Invoke(nameof(DelayedWinScreenText),1.5f);
                break;
            case GameState.Lose:
                _loseScreen.SetActive(true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
        }
    }

    void DelayedWinScreenText() {
        _winScreenText.SetActive(true);
    }

    void Update() {
        if(_state != GameState.WaitingInput) return;

        if(Input.GetKeyDown(KeyCode.LeftArrow)) Shift(Vector2.left);
        if(Input.GetKeyDown(KeyCode.RightArrow)) Shift(Vector2.right);
        if(Input.GetKeyDown(KeyCode.UpArrow)) Shift(Vector2.up);
        if(Input.GetKeyDown(KeyCode.DownArrow)) Shift(Vector2.down);
    }

    void GenerateGrid() {
        _round = 0;
        _nodes = new List<Node>();
        _blocks = new List<Block>();
        for (int x = 0; x < _width; x++) {
            for (int y = 0; y < _height; y++) {
                var node = Instantiate(_nodePrefab, new Vector2(x, y), Quaternion.identity);
                _nodes.Add(node);
            }
        }

        var center = new Vector2((float) _width /2 - 0.5f,(float) _height / 2 -0.5f);

        var board = Instantiate(_boardPrefab, center, Quaternion.identity);
        board.size = new Vector2(_width,_height);

        Camera.main.transform.position = new Vector3(center.x,center.y,-10);

        ChangeState(GameState.SpawningBlocks);
    }

    void SpawnBlocks(int amount) {

        var freeNodes = _nodes.Where(n => n.OccupiedBlock == null).OrderBy(b => Random.value).ToList();

        foreach (var node in freeNodes.Take(amount)) {
           SpawnBlock(node, Random.value > 0.8f ? 4 : 2);
        }

        if (freeNodes.Count() == 1) {
            ChangeState(GameState.Lose);
            return;
        }

        ChangeState(_blocks.Any(b=>b.Value == _winCondition) ? GameState.Win : GameState.WaitingInput);
    }

    void SpawnBlock(Node node, int value) {
        var block = Instantiate(_blockPrefab, node.Pos, Quaternion.identity);
        block.Init(GetBlockTypeByValue(value));
        block.SetBlock(node);
        _blocks.Add(block);
    }

    void Shift(Vector2 dir) {
        ChangeState(GameState.Moving);

        var orderedBlocks = _blocks.OrderBy(b => b.Pos.x).ThenBy(b => b.Pos.y).ToList();
        if (dir == Vector2.right || dir == Vector2.up) orderedBlocks.Reverse();

        foreach (var block in orderedBlocks) {
            var next = block.Node;
            do {
                block.SetBlock(next);

                var possibleNode = GetNodeAtPosition(next.Pos + dir);
                if (possibleNode != null) {
                    // We know a node is present
                    // If it's possible to merge, set merge
                    if (possibleNode.OccupiedBlock != null && possibleNode.OccupiedBlock.CanMerge(block.Value)) {
                        block.MergeBlock(possibleNode.OccupiedBlock);
                    }
                    // Otherwise, can we move to this spot?
                    else if (possibleNode.OccupiedBlock == null) next = possibleNode;

                    // None hit? End do while loop
                }
                

            } while (next != block.Node);
        }


        var sequence = DOTween.Sequence();

        foreach (var block in orderedBlocks) {
            var movePoint = block.MergingBlock != null ? block.MergingBlock.Node.Pos : block.Node.Pos;

            sequence.Insert(0, block.transform.DOMove(movePoint, _travelTime).SetEase(Ease.InQuad));
        }

        sequence.OnComplete(() => {
            var mergeBlocks = orderedBlocks.Where(b => b.MergingBlock != null).ToList();
            foreach (var block in mergeBlocks) {
                MergeBlocks(block.MergingBlock,block);
            }
            if(mergeBlocks.Any()) _source.PlayOneShot(_matchClips[Random.Range(0, _matchClips.Length)], 0.2f);
            ChangeState(GameState.SpawningBlocks);
        });


        _source.PlayOneShot(_moveClips[Random.Range(0,_moveClips.Length)],0.2f);
    }

    void MergeBlocks(Block baseBlock, Block mergingBlock) {
        var newValue = baseBlock.Value * 2;

        Instantiate(_mergeEffectPrefab, baseBlock.Pos, Quaternion.identity);
        Instantiate(_floatingTextPrefab, baseBlock.Pos, Quaternion.identity).Init(newValue);

        SpawnBlock(baseBlock.Node, newValue);

        RemoveBlock(baseBlock);
        RemoveBlock(mergingBlock);
    }

    void RemoveBlock(Block block) {
        _blocks.Remove(block);
        Destroy(block.gameObject);
    }

    Node GetNodeAtPosition(Vector2 pos) {
        return _nodes.FirstOrDefault(n => n.Pos == pos);
    }
   
}

[Serializable]
public struct BlockType {
    public int Value;
    public Color Color;
}

public enum GameState {
    GenerateLevel,
    SpawningBlocks,
    WaitingInput,
    Moving,
    Win,
    Lose
}