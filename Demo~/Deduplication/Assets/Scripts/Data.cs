using UnityEngine;

namespace Lumpn.Deduplication.Demo
{
    [CreateAssetMenu]
    public sealed class Data : ScriptableObject
    {
        [SerializeField] private Data[] dependencies;
    }
}
