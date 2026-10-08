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
        /// <summary>"number|car to avoid": the favour to make next, while it's being made (or if the game quit first).</summary>
        const string FavourNextKey = "ptt.favour.next";

        /// <summary>The next favour, being made on a worker thread while the postcard shows (null when there's none).</summary>
        System.Threading.Tasks.Task<LevelDef> pendingFavour;
        System.Diagnostics.Stopwatch pendingWatch;

        /// <summary>What closing the last favour cost the main thread (counting, saving, starting the next one), in ms.</summary>
        public static double LastFavourCloseMs { get; private set; } = -1;

        /// <summary>Work out every shape's turns on a worker at boot, so the first favour of a session costs what the rest do.</summary>
        static void WarmShapes()
        {
            var shapes = GameDatabase.Items.Select(d => d.Shape).ToList();
            System.Threading.Tasks.Task.Run(() =>
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                int turns = 0;
                foreach (var s in shapes) turns += s.Orientations().Count;
                Debug.Log($"[Favours] worked out {turns} turns of {shapes.Count} shapes on a worker in {watch.Elapsed.TotalMilliseconds:0.0} ms");
            });
        }

        /// <summary>
        /// Start making the next favour on a worker thread (the trips closed so far are read here, on the main thread);
        /// <see cref="PollFavour"/> saves it when it's done. Until then the save says which one to make, in case the game quits.
        /// </summary>
        void QueueFavour(int number, string avoidCar)
        {
            Prefs.DeleteKey(FavourKey);
            Prefs.SetString(FavourNextKey, number.ToString(CultureInfo.InvariantCulture) + "|" + (avoidCar ?? ""));
            var closed = GameDatabase.Levels.Where((l, i) => StarsFor(i) > 0).ToList();
            var pool = closed.SelectMany(l => l.Required.Concat(l.Bonus)).ToList();
            pendingWatch = System.Diagnostics.Stopwatch.StartNew();
            pendingFavour = System.Threading.Tasks.Task.Run(() => Favours.Make(number, closed, pool, avoidCar));
        }

        /// <summary>Save the favour the worker made, once it's done (called every frame; nothing to do most of the time).</summary>
        void PollFavour()
        {
            if (pendingFavour != null && pendingFavour.IsCompleted) AdoptFavour("on a worker while the postcard showed");
        }

        void AdoptFavour(string where)
        {
            var task = pendingFavour;
            pendingFavour = null;
            LevelDef favour = null;
            try { favour = task.Result; }
            catch (System.Exception e) { Debug.LogWarning("[Favours] making the next favour failed: " + e.GetBaseException().Message); }
            if (favour == null) return;
            Prefs.SetString(FavourKey, Favours.Serialize(favour));
            Prefs.DeleteKey(FavourNextKey);
            Prefs.Save();
            LogMade(favour, pendingWatch.Elapsed.TotalMilliseconds, where);
        }

        static void LogMade(LevelDef favour, double ms, string where)
        {
            int volume = favour.Required.Concat(favour.Bonus).Sum(d => d.Volume);
            Debug.Log($"[Favours] made favour {favour.Favour} for {favour.Sender}: {favour.Vehicle} ({favour.ModelId}), {favour.Required.Count} essentials + {favour.Bonus.Count} extras, " +
                      $"{volume} of {favour.FreeCells} cells ({volume / (float)favour.FreeCells:0.00}), {ms:0.0} ms {where}");
        }

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
            // Still being made (it takes a few ms): wait for it.
            if (pendingFavour != null)
            {
                pendingFavour.Wait();
                AdoptFavour("on a worker, waited for");
            }
            var data = Prefs.GetString(FavourKey, "");
            if (data.Length > 0)
            {
                var saved = Favours.Deserialize(data, GameDatabase.Levels, GameDatabase.ItemOrNull);
                if (saved != null) return saved;
                Debug.LogWarning("[Favours] the saved favour doesn't fit this version of the game, making a new one");
            }
            // The game quit while the next one was being made: make that one now.
            var next = Prefs.GetString(FavourNextKey, "").Split('|');
            if (next.Length == 2 && int.TryParse(next[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                return MakeFavour(number, next[1].Length > 0 ? next[1] : null);
            return MakeFavour(FavoursDone + 1, null);
        }

        /// <summary>Make favour <paramref name="number"/> from the trips closed so far, and save it.</summary>
        LevelDef MakeFavour(int number, string avoidCar, int variant = 0)
        {
            var closed = GameDatabase.Levels.Where((l, i) => StarsFor(i) > 0).ToList();
            var pool = closed.SelectMany(l => l.Required.Concat(l.Bonus)).ToList();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var favour = Favours.Make(number, closed, pool, avoidCar, variant);
            if (favour == null)
            {
                Debug.LogWarning($"[Favours] could not make favour {number}");
                return null;
            }
            Prefs.SetString(FavourKey, Favours.Serialize(favour));
            Prefs.DeleteKey(FavourNextKey);
            Prefs.Save();
            LogMade(favour, watch.Elapsed.TotalMilliseconds, "on the main thread");
            return favour;
        }

        /// <summary>
        /// A closed favour: count it (once), and if it was the one waiting, the next neighbour asks. That favour is made on a
        /// worker thread, so the postcard's first frame doesn't wait for it.
        /// </summary>
        void RecordFavour(LevelDef favour, int stars)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
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
            var current = pendingFavour != null ? null : Favours.Deserialize(Prefs.GetString(FavourKey, ""), GameDatabase.Levels, GameDatabase.ItemOrNull);
            bool next = pendingFavour == null && !Prefs.HasKey(FavourNextKey) && (current == null || current.Favour == favour.Favour);
            if (next) QueueFavour(favour.Favour + 1, favour.ModelId);
            Prefs.Save();
            LastFavourCloseMs = watch.Elapsed.TotalMilliseconds;
            Debug.Log($"[Favours] closing favour {favour.Favour} took {LastFavourCloseMs:0.00} ms on the main thread" + (next ? $"; favour {favour.Favour + 1} is being made on a worker" : ""));
        }

        /// <summary>
        /// ASK SOMEONE ELSE: the waiting favour goes to another neighbour, with another car (if there's a choice) and pile,
        /// under the same number. Its trunk is forgotten (the page asks first if anything's packed); the counts don't change.
        /// </summary>
        void SwapFavour()
        {
            var current = CurrentFavour();
            if (current == null) return;
            ForgetTrunk(current.Id);
            var next = MakeFavour(current.Favour, current.ModelId, current.FavourVariant + 1);
            if (next == null) return;
            Debug.Log($"[Favours] asked someone else: favour {next.Favour} is now {next.Sender}'s ({next.Title}, {next.Vehicle}), not {current.Sender}'s ({current.Title}, {current.Vehicle})");
            sfx.Page();
            ui.RefreshFavours(FavourPageInfo(true));
        }

        void ResetFavours()
        {
            var current = Prefs.GetString(FavourKey, "").Split('|');
            if (current.Length > 1) ForgetTrunk("favour-" + current[1]);
            pendingFavour = null;
            foreach (var key in new[] { FavourKey, FavoursDoneKey, FavoursThreeKey, FavourLastKey, FavourNextKey }) Prefs.DeleteKey(key);
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
