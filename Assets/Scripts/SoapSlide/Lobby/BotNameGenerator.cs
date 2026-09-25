using UnityEngine;

namespace SoapSlide.Lobby
{
    public static class BotNameGenerator
    {
        static readonly string[] s_PartsA =
        {
            "Slippery", "Soapy", "Bubble", "Slick", "Foam", "Wax", "Lather", "Rinse",
            "Squishy", "Glossy", "Zesty", "Fuzzy", "Tiny", "Grand", "Swift", "Lucky"
        };

        static readonly string[] s_PartsB =
        {
            "Penguin", "Moose", "Pickle", "Noodle", "Waffle", "Gizmo", "Pixel", "Rocket",
            "Mango", "Cactus", "Otter", "Badger", "Quokka", "Llama", "Newt", "Gecko"
        };

        public static string Next(System.Random rng)
        {
            string a = s_PartsA[rng.Next(s_PartsA.Length)];
            string b = s_PartsB[rng.Next(s_PartsB.Length)];
            int n = rng.Next(10, 99);
            return $"{a}{b}{n}";
        }

        public static string FakeQueueName(System.Random rng)
        {
            return $"Player{rng.Next(1000, 9999)}";
        }
    }
}
