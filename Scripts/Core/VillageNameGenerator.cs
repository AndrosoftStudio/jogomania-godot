using System;
using System.Collections.Generic;

namespace Jogomania.Core
{
    /// <summary>
    /// Gerador procedural de nomes de aldeias medievais/fantasia.
    /// </summary>
    public static class VillageNameGenerator
    {
        private static readonly string[] Prefixes = {
            "Ash", "Black", "Bright", "Brook", "Bur", "Cast", "Clif", "Cold",
            "Crom", "Cross", "Dark", "Dead", "Dun", "East", "Elder", "Elm",
            "Far", "Fen", "Ford", "Glen", "Gold", "Grand", "Green", "Grey",
            "Grim", "Hal", "Har", "Haven", "High", "Hill", "Hol", "Horn",
            "Iron", "Keld", "Lan", "Lock", "Long", "Low", "Mal", "Mar",
            "Mead", "Mid", "Mill", "Mor", "Nether", "New", "Night", "North",
            "Oak", "Old", "Over", "Pine", "Ral", "Ram", "Rath", "Raun",
            "Red", "Rock", "Sand", "Sil", "Silver", "Skel", "South", "Star",
            "Stone", "Storm", "Sul", "Tal", "Thorn", "Thunder", "Twin",
            "Under", "Uther", "Val", "Wald", "War", "West", "White", "Wild",
            "Wind", "Winter", "Witch", "Wolf", "Wood", "Wynn"
        };

        private static readonly string[] Suffixes = {
            "acre", "bank", "barrow", "bay", "borough", "bridge", "brook", "burn",
            "bury", "by", "castle", "cliff", "croft", "cross", "dale", "den",
            "dene", "ditch", "don", "down", "drift", "edge", "end", "fall",
            "fen", "ferry", "field", "firth", "fold", "ford", "forest", "forge",
            "fort", "gate", "glen", "green", "grove", "guard", "gulch", "hall",
            "ham", "haven", "head", "heath", "helm", "hill", "hold", "hollow",
            "holm", "holt", "home", "hope", "house", "hurst", "keep", "kirk",
            "knoll", "lake", "land", "lea", "lock", "loch", "mead", "mere",
            "mill", "mire", "moor", "mount", "mouth", "myre", "ness", "nook",
            "peak", "pool", "port", "reach", "rest", "ridge", "rise", "rift",
            "rock", "run", "sea", "shade", "shore", "side", "spire", "spring",
            "stand", "stead", "steep", "stone", "stream", "tor", "town",
            "vale", "vault", "view", "wald", "wall", "ward", "watch", "water",
            "well", "wend", "wick", "wood", "worth"
        };

        private static readonly string[] Middles = {
            "", "", "", "", // mais chances de não ter middle (nomes curtos são mais bonitos)
            "a", "bridge", "cross", "end", "gate", "green", "hill", "lea",
            "le", "mere", "of the", "on the", "over", "under", "upon"
        };

        private static readonly Random _rng = new Random();
        private static readonly HashSet<string> _usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Reset()
        {
            _usedNames.Clear();
        }

        public static string GenerateName(int seed)
        {
            var rnd = new Random(seed);
            
            for (int attempt = 0; attempt < 20; attempt++)
            {
                string name = BuildName(rnd);
                if (!_usedNames.Contains(name))
                {
                    _usedNames.Add(name);
                    return name;
                }
            }
            // Fallback com número se tudo já foi usado
            return BuildName(rnd) + " " + (seed % 1000);
        }

        private static string BuildName(Random rnd)
        {
            string prefix = Prefixes[rnd.Next(Prefixes.Length)];
            string suffix = Suffixes[rnd.Next(Suffixes.Length)];
            
            // 30% de chance de ter um "middle" element
            if (rnd.NextDouble() < 0.3)
            {
                string middle = Middles[rnd.Next(Middles.Length)];
                if (middle.Length > 0)
                    return $"{prefix}{middle}{suffix}";
            }
            
            return $"{prefix}{suffix}";
        }

        /// <summary>Gera um lote de nomes únicos para as aldeias do mapa.</summary>
        public static List<string> GenerateBatch(int count)
        {
            Reset();
            var names = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                names.Add(GenerateName(i * 7919 + 31337)); // primes para dispersão
            }
            return names;
        }
    }
}
