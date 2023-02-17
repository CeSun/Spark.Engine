using System.Collections.Immutable;

Dictionary<string, int> map = new Dictionary<string, int>();

var list = (from kv in map select (kv.Value, kv.Key)).ToArray();

var list2 = (from kv in map where kv.Value > 100 select (kv.Value, kv.Value)).ToArray();

var dictionary = (from kv in map where kv.Value > 100 select kv).ToDictionary(kv => kv.Key, kv => kv.Value);