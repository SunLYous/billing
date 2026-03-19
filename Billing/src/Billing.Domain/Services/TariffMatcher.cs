namespace Billing.Domain.Services;

using Billing.Domain.Models;

public sealed class TariffMatcher
{
    private readonly TrieNode _root = new();

    public void Build(IReadOnlyList<Tariff> tariffs)
    {
        foreach (var t in tariffs)
        {
            var node = _root;
            foreach (var ch in t.Prefix)
            {
                node = node.GetOrAdd(ch);
            }
            node.AddTariff(t);
        }
    }

    public Tariff? Match(string calledNumber, DateTime callTime)
    {
        Tariff? best = FindBestActive(_root, callTime);
        var node = _root;

        foreach (var ch in calledNumber)
        {
            if (!node.TryGet(ch, out var next))
                break;

            node = next!;
            var candidate = FindBestActive(node, callTime);
            if (candidate is not null)
                best = candidate;
        }

        return best;
    }

    private static Tariff? FindBestActive(TrieNode node, DateTime callTime)
    {
        Tariff? best = null;
        foreach (var t in node.Tariffs)
        {
            if (!t.IsActiveAt(callTime))
                continue;
            if (best is null || t.Priority > best.Priority)
                best = t;
        }
        return best;
    }

    private sealed class TrieNode
    {
        private readonly TrieNode?[] _children = new TrieNode?[10];
        private Dictionary<char, TrieNode>? _extra;
        private List<Tariff>? _tariffs;

        public IReadOnlyList<Tariff> Tariffs => _tariffs ?? (IReadOnlyList<Tariff>)Array.Empty<Tariff>();

        public void AddTariff(Tariff t)
        {
            _tariffs ??= new List<Tariff>();
            _tariffs.Add(t);
        }

        public TrieNode GetOrAdd(char ch)
        {
            if (ch is >= '0' and <= '9')
            {
                var idx = ch - '0';
                return _children[idx] ??= new TrieNode();
            }

            _extra ??= new Dictionary<char, TrieNode>();
            if (!_extra.TryGetValue(ch, out var node))
            {
                node = new TrieNode();
                _extra[ch] = node;
            }
            return node;
        }

        public bool TryGet(char ch, out TrieNode? node)
        {
            if (ch is >= '0' and <= '9')
            {
                node = _children[ch - '0'];
                return node is not null;
            }

            if (_extra is not null)
                return _extra.TryGetValue(ch, out node);

            node = null;
            return false;
        }
    }
}
