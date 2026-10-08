using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PackTheTrunk
{
    public partial class AutoPilot
    {
        /// <summary>
        /// A favour checked with the game's own rules: its pile is what its packing packs, from closed trips only, and
        /// every placement, in the order the pile was made, passes <see cref="TrunkGrid.Check"/>, so the favour can be
        /// packed 100%. Null if it's fine.
        /// </summary>
        static string FavourProblem(LevelDef f, ICollection<string> pool)
        {
            if (f.Packing == null || f.Packing.Count == 0) return "no packing";
            var pile = f.Required.Concat(f.Bonus).Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var packed = f.Packing.Select(p => p.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (!pile.SequenceEqual(packed)) return "the packing packs different things than the pile";
            var outside = pile.Where(id => !pool.Contains(id)).Distinct().ToList();
            if (outside.Count > 0) return "not from a closed trip: " + string.Join(", ", outside);
            if (f.FreeCells < Favours.MinFreeCells) return $"a trunk of {f.FreeCells} cells";
            var grid = new TrunkGrid(f.Size, f.Blocked);
            foreach (var (id, cells) in f.Packing)
            {
                var def = GameDatabase.ItemOrNull(id);
                var min = cells.Aggregate(Vector3Int.Min);
                var local = new HashSet<Vector3Int>(cells.Select(c => c - min));
                var turn = def.Shape.Orientations().FirstOrDefault(o => o.Shape.Voxels.Length == local.Count && o.Shape.Voxels.All(v => local.Contains(v.Pos)));
                if (turn.Shape == null) return $"{id} isn't a turn of its shape";
                var result = grid.Check(turn.Shape, min, def.Fragile, out _);
                if (result != PlacementResult.Ok) return $"{id} at {min}: {result}";
                grid.PlaceData(def.Fragile, turn.Shape, min);
            }
            float fill = f.Packing.Sum(p => p.Cells.Count) / (float)f.FreeCells;
            if (fill < Favours.MinFill - 0.001f || fill > Favours.MaxFill + 0.001f) return $"fill {fill:0.00}";
            if (f.Packing.Count < Favours.MinItems || f.Packing.Count > Favours.MaxItems) return $"{f.Packing.Count} things";
            if (f.Bonus.Count < 2) return "fewer than two extras";
            return null;
        }

        static bool SamePile(LevelDef a, LevelDef b) =>
            a.Required.Concat(a.Bonus).Select(d => d.Id).SequenceEqual(b.Required.Concat(b.Bonus).Select(d => d.Id)) && a.ModelId == b.ModelId;

        /// <summary>
        /// The favour maker on its own: hundreds of favours from the trips closed so far, each checked with
        /// <see cref="FavourProblem"/> and timed, and written to favours.json for <c>solve_levels.py --check-favours</c>.
        /// </summary>
        IEnumerator FavourMakerChecks()
        {
            var closed = GameDatabase.Levels.Where(l => Prefs.GetInt("ptt.stars." + l.Id, 0) > 0).ToList();
            var pool = closed.SelectMany(l => l.Required.Concat(l.Bonus)).ToList();
            var poolIds = new HashSet<string>(pool.Select(d => d.Id));
            var cars = closed.Where(l => l.FreeCells >= Favours.MinFreeCells).ToList();
            int count = quick ? 80 : 300;
            var problems = new List<string>();
            var times = new List<double>();
            var fills = new List<float>();
            var used = new HashSet<string>();
            var inv = CultureInfo.InvariantCulture;
            var json = new StringBuilder("[");
            string poolJson = string.Join(",", poolIds.OrderBy(x => x, StringComparer.Ordinal).Select(id => "\"" + id + "\""));
            for (int i = 0; i < count; i++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                // Numbers far from the save's own favours.
                var f = Favours.Make(10001 + i, cars, pool);
                times.Add(watch.Elapsed.TotalMilliseconds);
                if (f == null) { problems.Add($"favour {10001 + i} wasn't made"); continue; }
                used.Add(f.ModelId);
                fills.Add(f.Packing.Sum(p => p.Cells.Count) / (float)f.FreeCells);
                var problem = FavourProblem(f, poolIds);
                if (problem != null) problems.Add($"favour {f.Favour} ({f.ModelId}): {problem}");
                if (i > 0) json.Append(',');
                json.Append("{\"number\":").Append(f.Favour.ToString(inv)).Append(",\"car\":\"").Append(f.ModelId).Append("\",\"required\":[")
                    .Append(string.Join(",", f.Required.Select(d => "\"" + d.Id + "\""))).Append("],\"bonus\":[")
                    .Append(string.Join(",", f.Bonus.Select(d => "\"" + d.Id + "\""))).Append("],\"pool\":[").Append(poolJson).Append("],\"packing\":[")
                    .Append(string.Join(",", f.Packing.Select(p => "{\"id\":\"" + p.Id + "\",\"cells\":[" +
                        string.Join(",", p.Cells.Select(c => $"[{c.x.ToString(inv)},{c.y.ToString(inv)},{c.z.ToString(inv)}]")) + "]}")))
                    .Append("]}");
                if (i % 10 == 9) yield return null;
            }
            json.Append(']');
            File.WriteAllText(Path.Combine(outDir, "favours.json"), json.ToString());
            PerfProbe.Ignore();
            Check(problems.Count == 0 && times.Count == count,
                $"favours: made {count} from {closed.Count} closed trips ({poolIds.Count} things), in {used.Count} of {cars.Count} cars, " +
                $"{times.Average():0.0} ms on average and {times.Max():0.0} ms at most, fill {fills.DefaultIfEmpty().Min():0.00}-{fills.DefaultIfEmpty().Average():0.00}-{fills.DefaultIfEmpty().Max():0.00}; " +
                (problems.Count == 0 ? "every one is drawn from closed trips and packs completely under TrunkGrid's rules (favours.json)" : string.Join("; ", problems.Take(5))));
        }

        IEnumerator FlipToNeighbours()
        {
            for (int i = 0; i <= GameDatabase.Chapters.Count; i++)
            {
                yield return ClickUi("Next Page");
                yield return Wait(0.3f);
            }
            yield return Wait(1.4f);
        }

        int PackedCount => game.Items.Count(i => i.State == ItemState.Packed || i.State == ItemState.Dropping);

        IEnumerator WaitForResults()
        {
            for (float t = 0f; t < 10f && !(game.IsShowingResults && game.Ui.ResultsReady); t += Time.unscaledDeltaTime) yield return null;
            yield return Wait(2.6f);
        }

        IEnumerator WaitForStory()
        {
            for (float t = 0f; t < 6f && !game.IsInStory; t += Time.unscaledDeltaTime) yield return null;
            yield return Wait(2.5f);
        }

        /// <summary>
        /// Favours for the neighbours through the real UI: the map's last page locked, then open; the pin, the texts and
        /// a favour packed to 100% by hints alone, for three stars with no seal or photo; NEXT FAVOUR makes a new pile;
        /// leaving it half-packed and coming back restores it; TRY AGAIN replays the same pile; with every trip packed,
        /// CONTINUE goes to the neighbours; the favour reset clears them. The save's stars are put back afterwards.
        /// </summary>
        IEnumerator FavourChecks()
        {
            var savedStars = GameDatabase.Levels.ToDictionary(l => l.Id, l => Prefs.GetInt("ptt.stars." + l.Id, 0));
            int unlockAt = LevelIndex(Favours.UnlockedBy);
            game.AutoResetFavours();

            // Locked until Everything But... is packed.
            Prefs.SetInt("ptt.stars." + Favours.UnlockedBy, 0);
            PerfProbe.Begin("menus");
            game.AutoShowMenu();
            yield return Wait(1.8f);
            yield return FlipToNeighbours();
            var pin = FindButton("Favour Pin");
            bool locked = pin != null && !pin.interactable && AnyText("The neighbours start asking once") && game.AutoCurrentFavour() == null;
            yield return Shot("favours-locked");
            Check(locked, $"favours: until {GameDatabase.Levels[unlockAt].Title} is packed, the map's last page has a locked pin and says what opens it");

            // Open: the first nine trips packed (later ones as they were).
            for (int i = 0; i <= unlockAt; i++)
                if (Prefs.GetInt("ptt.stars." + GameDatabase.Levels[i].Id, 0) == 0) Prefs.SetInt("ptt.stars." + GameDatabase.Levels[i].Id, 1);
            yield return FavourMakerChecks();
            game.AutoShowMenu();
            yield return Wait(1.8f);
            yield return FlipToNeighbours();
            var first = game.AutoCurrentFavour();
            pin = FindButton("Favour Pin");
            bool open = first != null && first.Favour == 1 && pin != null && pin.interactable && AnyText(first.Sender);
            yield return Shot("favours-page");
            yield return ClickUi("Favour Pin");
            yield return WaitForStory();
            bool story = game.IsInStory && AnyText("THE NEIGHBOURS  ·  FAVOUR 1");
            yield return Shot("favours-story");
            yield return StoryToPacking();
            yield return Wait(1.5f);
            var level = game.CurrentLevelDef;
            int things = first != null ? first.Required.Count + first.Bonus.Count : -1;
            Check(open && story && game.IsPlaying && level != null && level.IsFavour && level.Favour == 1 && game.Items.Count == things,
                $"favours: once open, the page shows favour 1 ({first?.Sender}, {first?.Title}); its pin opens the texts, then packing {things} things in the {first?.Vehicle}");
            yield return Shot("favours-packing");

            PerfProbe.Begin("playing");
            yield return FollowHints();
            yield return Wait(0.8f);
            Check(PackedCount == game.Items.Count, $"favours: following only Grandpa's hints packs favour 1 to 100% ({PackedCount}/{game.Items.Count}, {followSteps} steps{(followStuck != null ? ", stuck: " + followStuck : "")})");
            int polaroids = GameDatabase.Levels.Count(l => game.AutoPhoto(l.Id) != null);
            PerfProbe.Begin("close + drive-off");
            yield return Press(Key.Space);
            yield return WaitForResults();
            Check(game.IsShowingResults && game.LastStars == 3 && !game.Ui.ResultsShowSeal && game.AutoPhoto(first.Id) == null
                  && GameDatabase.Levels.Count(l => game.AutoPhoto(l.Id) != null) == polaroids && GameController.FavoursDone == 1 && GameController.FavoursThreeStars == 1 && AnyText("NEXT FAVOUR"),
                $"favours: closing it gives three stars ({game.LastStars}), no seal and no album photo; {GameController.FavoursDone} done, {GameController.FavoursThreeStars} with three stars; the postcard offers NEXT FAVOUR");
            yield return Shot("favours-postcard");

            // NEXT FAVOUR: a new neighbour and a new pile.
            yield return ClickUi("Next");
            yield return WaitForStory();
            var second = game.CurrentLevelDef;
            yield return StoryToPacking();
            yield return Wait(1.5f);
            Check(second != null && second.IsFavour && second.Favour == 2 && !SamePile(first, second) && game.IsPlaying,
                $"favours: NEXT FAVOUR goes to favour 2 ({second?.Sender}, {second?.Vehicle}), with a different pile ({second?.Required.Count + second?.Bonus.Count} things)");

            // Leave it half-packed through the pause menu, come back through the map's pin.
            for (int k = 0; k < 3; k++)
            {
                var hint = game.AutoFindHint();
                if (hint.Item != null) game.AutoPlace(hint.Item, hint.Rotation, hint.Pos);
                yield return Wait(0.2f);
            }
            yield return Wait(0.6f);
            int half = PackedCount;
            var layout = game.Items.Where(i => i.State == ItemState.Packed).Select(i => i.Def.Id + "@" + i.GridPos).OrderBy(x => x, StringComparer.Ordinal).ToList();
            yield return Press(Key.Escape);
            yield return Wait(1f);
            yield return ClickUi("Pause Map");
            yield return Wait(2.5f);
            bool waiting = AnyText($"{half} PACKED, WAITING");
            yield return Shot("favours-waiting");
            yield return ClickUi("Favour Pin");
            yield return WaitForStory();
            bool back = AnyText("BACK TO PACKING");
            yield return StoryToPacking();
            yield return Wait(1.5f);
            var again = game.Items.Where(i => i.State == ItemState.Packed).Select(i => i.Def.Id + "@" + i.GridPos).OrderBy(x => x, StringComparer.Ordinal).ToList();
            Check(half == 3 && waiting && back && game.CurrentLevelDef?.Favour == 2 && again.SequenceEqual(layout),
                $"favours: leaving favour 2 with {half} packed through the pause menu, the page says it's waiting, and the pin brings back the same {again.Count} in the same cells");

            // Finish it, then TRY AGAIN: the same pile again, and nothing counted twice.
            yield return FollowHints();
            yield return Wait(0.8f);
            yield return Press(Key.Space);
            yield return WaitForResults();
            int doneAfter = GameController.FavoursDone;
            yield return ClickUi("Retry");
            for (float t = 0f; t < 6f && !game.IsPlaying; t += Time.unscaledDeltaTime) yield return null;
            yield return Wait(1.5f);
            var retry = game.CurrentLevelDef;
            var current = game.AutoCurrentFavour();
            Check(doneAfter == 2 && retry != null && retry.Favour == 2 && SamePile(retry, second) && PackedCount == 0 && current?.Favour == 3,
                $"favours: TRY AGAIN replays favour 2 with the same pile, fresh ({PackedCount} packed); {doneAfter} done, and favour {current?.Favour} is the one waiting next");

            // With every trip packed, the title's CONTINUE goes to the neighbours.
            foreach (var l in GameDatabase.Levels) if (Prefs.GetInt("ptt.stars." + l.Id, 0) == 0) Prefs.SetInt("ptt.stars." + l.Id, 1);
            PerfProbe.Begin("menus");
            game.AutoShowMainMenu();
            yield return Wait(1.6f);
            bool line = AnyText("The neighbours need a hand");
            yield return ClickUi("Continue");
            yield return WaitForStory();
            Check(line && game.IsInStory && game.CurrentLevelDef?.Favour == 3,
                $"favours: with every trip packed, the title says the neighbours need a hand and CONTINUE opens favour {game.CurrentLevelDef?.Favour}");
            yield return Shot("favours-continue");

            // Erasing progress clears them (the reset it runs).
            game.AutoResetFavours();
            Check(GameController.FavoursDone == 0 && GameController.FavoursThreeStars == 0 && GameController.SavedFavour == "",
                "favours: erasing progress's favour reset clears the counts and the waiting favour");

            foreach (var kv in savedStars) Prefs.SetInt("ptt.stars." + kv.Key, kv.Value);
            game.AutoResetFavours();
            game.AutoShowMainMenu();
            yield return Wait(1f);
        }
    }
}
