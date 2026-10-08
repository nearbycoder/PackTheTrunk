using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace PackTheTrunk
{
    /// <summary>
    /// Favours for the neighbours (the trip map's last page): once chapter II is packed, the neighbours ask for a hand
    /// with a car you've packed and a pile made for it (<see cref="Favours"/>). One favour waits at a time; closing it
    /// makes the next. Favours don't take album photos, give seals or count towards the trips.
    /// </summary>
    public partial class GameController
    {
        const string FavourKey = "ptt.favour", FavoursDoneKey = "ptt.favours.done", FavoursThreeKey = "ptt.favours.three", FavourLastKey = "ptt.favour.last";

        static int TripIndex(string id)
        {
            var levels = GameDatabase.Levels;
            for (int i = 0; i < levels.Count; i++) if (levels[i].Id == id) return i;
            return -1;
        }

        public bool FavoursUnlocked
        {
            get
            {
                int i = TripIndex(Favours.UnlockedBy);
                return i >= 0 && StarsFor(i) > 0;
            }
        }

        public static int FavoursDone => Prefs.GetInt(FavoursDoneKey, 0);
        public static int FavoursThreeStars => Prefs.GetInt(FavoursThreeKey, 0);

        /// <summary>The neighbour who needs a hand now: the saved favour, or a new one (then saved). Null while locked.</summary>
        public LevelDef CurrentFavour()
        {
            if (!FavoursUnlocked) return null;
            var data = Prefs.GetString(FavourKey, "");
            if (data.Length > 0)
            {
                var saved = Favours.Deserialize(data, GameDatabase.Levels, GameDatabase.ItemOrNull);
                if (saved != null) return saved;
                Debug.LogWarning("[Favours] the saved favour doesn't fit this version of the game, making a new one");
            }
            return MakeFavour(FavoursDone + 1, null);
        }

        /// <summary>Make favour <paramref name="number"/> from the trips closed so far, and save it.</summary>
        LevelDef MakeFavour(int number, string avoidCar)
        {
            var closed = GameDatabase.Levels.Where((l, i) => StarsFor(i) > 0).ToList();
            var pool = closed.SelectMany(l => l.Required.Concat(l.Bonus)).ToList();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var favour = Favours.Make(number, closed, pool, avoidCar);
            if (favour == null)
            {
                Debug.LogWarning($"[Favours] could not make favour {number}");
                return null;
            }
            Prefs.SetString(FavourKey, Favours.Serialize(favour));
            Prefs.Save();
            int volume = favour.Required.Concat(favour.Bonus).Sum(d => d.Volume);
            Debug.Log($"[Favours] made favour {number} for {favour.Sender}: {favour.Vehicle} ({favour.ModelId}), {favour.Required.Count} essentials + {favour.Bonus.Count} extras, " +
                      $"{volume} of {favour.FreeCells} cells ({volume / (float)favour.FreeCells:0.00}), {watch.Elapsed.TotalMilliseconds:0.0} ms");
            return favour;
        }

        /// <summary>A closed favour: count it (once), and if it was the one waiting, the next neighbour asks.</summary>
        void RecordFavour(LevelDef favour, int stars)
        {
            var last = Prefs.GetString(FavourLastKey, "0,0").Split(',');
            int lastNumber = int.Parse(last[0], CultureInfo.InvariantCulture), lastStars = int.Parse(last[1], CultureInfo.InvariantCulture);
            if (favour.Favour != lastNumber)
            {
                Prefs.SetInt(FavoursDoneKey, FavoursDone + 1);
                if (stars == 3) Prefs.SetInt(FavoursThreeKey, FavoursThreeStars + 1);
                lastStars = stars;
            }
            else if (stars == 3 && lastStars < 3)
            {
                Prefs.SetInt(FavoursThreeKey, FavoursThreeStars + 1);
                lastStars = 3;
            }
            Prefs.SetString(FavourLastKey, $"{favour.Favour.ToString(CultureInfo.InvariantCulture)},{Mathf.Max(lastStars, stars).ToString(CultureInfo.InvariantCulture)}");
            Debug.Log($"[Favours] favour {favour.Favour} closed for {stars} star{(stars == 1 ? "" : "s")}: {FavoursDone} done, {FavoursThreeStars} with three stars");
            var current = Favours.Deserialize(Prefs.GetString(FavourKey, ""), GameDatabase.Levels, GameDatabase.ItemOrNull);
            if (current == null || current.Favour == favour.Favour) MakeFavour(favour.Favour + 1, favour.ModelId);
            Prefs.Save();
        }

        void ResetFavours()
        {
            var current = Prefs.GetString(FavourKey, "").Split('|');
            if (current.Length > 1) ForgetTrunk("favour-" + current[1]);
            foreach (var key in new[] { FavourKey, FavoursDoneKey, FavoursThreeKey, FavourLastKey }) Prefs.DeleteKey(key);
        }

        /// <summary>The trip map's neighbours page: whether it's open, who's waiting, and the totals.</summary>
        GameUI.FavourPage FavourPageInfo(bool openHere)
        {
            var favour = CurrentFavour();
            return new GameUI.FavourPage
            {
                Unlocked = favour != null,
                LockedBy = GameDatabase.Levels[Mathf.Max(0, TripIndex(Favours.UnlockedBy))],
                Favour = favour,
                Waiting = favour != null ? SavedTrunkCount(favour.Id) : 0,
                Done = FavoursDone,
                ThreeStars = FavoursThreeStars,
                OpenHere = openHere,
            };
        }
    }
}
