using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PackTheTrunk
{
    /// <summary>
    /// The family album's close-up: click a polaroid to see that trunk's photo big, with the trip,
    /// the year, the car, the stars and the seal, then flip through the others (arrows, A / D, the
    /// D-pad or the bumpers). Escape, B or a click outside the photo goes back to the album.
    /// </summary>
    public partial class GameUI
    {
        const float ZoomCardWidth = 980f, ZoomCardHeight = 900f, ZoomArrowGap = 110f;

        RectTransform albumZoom, zoomFrame;
        RawImage zoomPhoto;
        Text zoomTitle, zoomWhen, zoomCount;
        Image[] zoomStars;
        RectTransform zoomSeal;
        Func<int, int> albumStarsFor;
        Func<string, Texture2D> albumFullPhotoFor;
        readonly List<(LevelDef Level, Texture2D Photo)> albumPhotos = new List<(LevelDef, Texture2D)>();
        int zoomIndex = -1;

        /// <summary>Every polaroid is down and the album's button is showing (for the self-test).</summary>
        public bool AlbumRevealed => IsAlbumOpen && albumDone != null && albumDone.gameObject.activeInHierarchy;

        public bool IsAlbumZoomOpen => albumZoom != null && albumZoom.gameObject.activeSelf;

        /// <summary>The trip whose photo the close-up shows (for the self-test).</summary>
        public string AlbumZoomTrip => IsAlbumZoomOpen && zoomIndex >= 0 ? albumPhotos[zoomIndex].Level.Id : null;

        /// <summary>The close-up's photo, in pixels (for the self-test).</summary>
        public Vector2Int AlbumZoomTextureSize => IsAlbumZoomOpen && zoomPhoto.texture != null ? new Vector2Int(zoomPhoto.texture.width, zoomPhoto.texture.height) : Vector2Int.zero;

        /// <summary>The close-up photo's width on screen, in pixels (for the self-test).</summary>
        public float AlbumZoomPhotoWidth => IsAlbumZoomOpen ? ScreenRect(zoomPhoto.rectTransform).width : 0f;

        void BuildAlbumZoom()
        {
            albumZoom = UiKit.Rect("Album Zoom", album).Fill();
            var dim = UiKit.Image("Album Zoom Dim", albumZoom, new Color(0.08f, 0.06f, 0.05f, 0.85f), false);
            dim.rectTransform.Fill();
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => { Sfx.Instance?.Back(); CloseAlbumZoom(); });

            // Everything else sits in one frame that shrinks to fit short screens and big interface sizes.
            zoomFrame = UiKit.Rect("Frame", albumZoom).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(ZoomCardWidth + 2f * ZoomArrowGap, ZoomCardHeight));
            var card = UiTheme.Card("Zoom Polaroid", zoomFrame, Color.white, -1.2f);
            ((RectTransform)card.parent).Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ZoomCardWidth, ZoomCardHeight));
            // The card catches clicks, so clicking the photo itself doesn't close it.
            card.GetComponent<Image>().raycastTarget = true;
            UiTheme.Tape(card, new Vector2(0.5f, 1f), new Vector2(0, -6), 2f, 200f);

            zoomPhoto = UiKit.Rect("Photo", card).gameObject.AddComponent<RawImage>();
            zoomPhoto.raycastTarget = false;
            zoomPhoto.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -720), new Vector2(-30, -30));
            zoomTitle = UiTheme.Label("Title", card, "", UiTheme.Hand, 54, UiTheme.Ink, TextAnchor.UpperLeft);
            zoomTitle.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(40, 96), new Vector2(-250, 166));
            zoomWhen = UiTheme.Label("When", card, "", UiTheme.Body, 25, UiTheme.InkSoft, TextAnchor.UpperLeft);
            zoomWhen.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(42, 40), new Vector2(-250, 92));

            var stars = UiKit.Rect("Stars", card).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-236, 104), new Vector2(-40, 156));
            UiKit.Horizontal(stars.gameObject, 6, TextAnchor.MiddleRight);
            zoomStars = new Image[3];
            for (int i = 0; i < 3; i++) zoomStars[i] = UiKit.StarImage(stars, true, 52);
            zoomSeal = UiTheme.Seal(card, 96);
            zoomSeal.Pin(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-34, -34), new Vector2(96, 96));
            zoomCount = UiTheme.Label("Count", card, "", UiTheme.Body, 21, UiTheme.InkSoft, TextAnchor.UpperRight);
            zoomCount.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-300, 40), new Vector2(-42, 80));

            var prev = UiTheme.Pill("Album Zoom Prev", zoomFrame, "<", UiTheme.Night, 34, () => FlipAlbumZoom(-1));
            ((RectTransform)prev.transform).Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(76, 76));
            var next = UiTheme.Pill("Album Zoom Next", zoomFrame, ">", UiTheme.Night, 34, () => FlipAlbumZoom(1));
            ((RectTransform)next.transform).Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(76, 76));
            prev.GetComponent<PillHover>().Silent = true;
            next.GetComponent<PillHover>().Silent = true;
            albumZoom.gameObject.SetActive(false);
        }

        /// <summary>A polaroid with a photo: clicking it opens the close-up.</summary>
        void MakePolaroidClickable(RectTransform holder, Image card, LevelDef level, Texture2D photo)
        {
            int index = albumPhotos.Count;
            albumPhotos.Add((level, photo));
            holder.name = "Polaroid " + level.Id;
            card.raycastTarget = true;
            var button = holder.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OpenAlbumZoom(index));
            holder.gameObject.AddComponent<PillHover>().HoverScale = 1.08f;
        }

        void OpenAlbumZoom(int index)
        {
            if (index < 0 || index >= albumPhotos.Count) return;
            float fit = Mathf.Min(1f, (root.rect.height - 40f) / ZoomCardHeight, (root.rect.width - 40f) / (ZoomCardWidth + 2f * ZoomArrowGap));
            zoomFrame.localScale = Vector3.one * fit;
            albumZoom.gameObject.SetActive(true);
            albumZoom.SetAsLastSibling();
            ShowAlbumZoom(index);
        }

        void ShowAlbumZoom(int index)
        {
            zoomIndex = index;
            var (level, photo) = albumPhotos[index];
            // The polaroids use a small copy; the close-up loads the full photo.
            zoomPhoto.texture = albumFullPhotoFor?.Invoke(level.Id) ?? photo;
            zoomTitle.text = level.Title;
            string when = string.IsNullOrEmpty(level.Trip) ? "" : level.Trip;
            if (level.Year > 0) when += (when.Length > 0 ? ", " : "") + level.Year;
            zoomWhen.text = $"{when}  ·  {level.Vehicle}";
            int stars = albumStarsFor != null ? albumStarsFor(level.Index) : 0;
            for (int i = 0; i < zoomStars.Length; i++)
                zoomStars[i].color = i < stars ? UiTheme.Gold : new Color(UiTheme.InkSoft.r, UiTheme.InkSoft.g, UiTheme.InkSoft.b, 0.18f);
            zoomSeal.gameObject.SetActive(SealedFor != null && SealedFor(level.Index));
            zoomCount.text = albumPhotos.Count > 1 ? $"photo {index + 1} of {albumPhotos.Count}" : "";
        }

        void FlipAlbumZoom(int step)
        {
            if (!IsAlbumZoomOpen || albumPhotos.Count < 2) return;
            Sfx.Instance?.Page();
            ShowAlbumZoom((zoomIndex + step + albumPhotos.Count) % albumPhotos.Count);
        }

        void CloseAlbumZoom()
        {
            if (albumZoom != null) albumZoom.gameObject.SetActive(false);
            zoomIndex = -1;
        }

        /// <summary>Flipping through the close-up from the keyboard or a pad (Escape and B are handled with the other overlays).</summary>
        void UpdateAlbumZoom()
        {
            if (!IsAlbumZoomOpen) return;
            var kb = Keyboard.current;
            bool left = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) ||
                        Pad.Down(p => p.dpad.left) || Pad.Down(p => p.leftShoulder);
            bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) ||
                         Pad.Down(p => p.dpad.right) || Pad.Down(p => p.rightShoulder);
            if (left) FlipAlbumZoom(-1);
            else if (right) FlipAlbumZoom(1);
        }
    }
}
