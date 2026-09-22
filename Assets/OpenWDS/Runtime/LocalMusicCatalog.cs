using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWDS.Runtime
{
    public enum MusicDifficulty
    {
        None = 0,
        Normal = 1,
        Hard = 2,
        Extra = 3,
        Stella = 4,
        Olivier = 5,
    }

    public enum LiveType
    {
        Normal = 1,
        Multi = 2,
        Lesson = 3,
        Audition = 4,
        League = 5,
        MultiCollectionEvent = 6,
        Concert = 7,
        BonusLive = 8,
        CourseMode = 9,
        TripleCast = 10,
        GhostLive = 11,
        Trial = 12,
        MultiRoom = 13,
    }

    public enum MusicVideoType
    {
        None = 0,
        RealTimeRendering = 1,
        Movie = 2,
    }

    /// <summary>
    /// Local representation of the MusicMaster + LiveMaster records consumed by
    /// MusicSelectionContentEntity. Local asset paths are an explicit offline recovery
    /// adapter; the original Android release factories always use their Web URLs.
    /// </summary>
    [Serializable]
    public sealed class LocalMusicEntry
    {
        public long Id;
        public string Name;
        public string Description;
        public string PronounceName;
        // Nullable ReleasedAt and group-main ID projected by
        // tools/sync_music_sort_metadata.py from the original Master tables.
        public bool HasReleasedAt;
        public long ReleasedAtUtcTicks;
        public bool HasSortReleasedAt;
        public long SortReleasedAtUtcTicks;
        public long SortMusicId;
        public string LyricWriter;
        public string Composer;
        public string Arranger;
        public int MusicCoverType;
        public string MusicTypeName => MusicCoverType == 1 ? "ORIGINAL" :
            MusicCoverType == 2 ? "COVER" :
            throw new FormatException("Unknown MusicCoverType: " + MusicCoverType);
        public string Vocals;
        public long[] ActorIds;
        public bool Invisible;
        public bool IsAvailable => IsAvailableAt(DateTime.UtcNow);
        public bool IsAvailableAt(DateTime utcNow) => !Invisible &&
            (!HasReleasedAt || ReleasedAtUtcTicks <= utcNow.Ticks);
        public bool IsLongVersion;
        public int StaminaConsumption;
        public int MusicTimeSecond;
        public float SampleStartSeconds;
        public float SampleEndSeconds;
        public float DelaySeconds;
        public int VocalVersion = 1;
        public MusicVideoType MusicVideoType;
        public string JacketAssetPath;
        public string MusicAcbPath;
        public string PreviewAcbPath;
        public string MusicCue;
        public LocalLiveEntry[] Lives;
    }

    [Serializable]
    public sealed class LocalCharacterBaseEntry
    {
        public long Id;
        public string Name;
        public string ShortName;
        public int Company;
    }

    [Serializable]
    public sealed class LocalLiveEntry
    {
        public long AnotherNotationId;
        public long Id;
        public MusicDifficulty Difficulty;
        public int Level;
        public int NoteCount;
        public string DebugNotationAssetPath;
        public string DebugMusicConfigAssetPath;
    }

    [Serializable]
    public sealed class LocalMusicCatalog
    {
        public LocalMusicEntry[] Musics;
        public LocalCharacterBaseEntry[] CharacterBases;

        public static LocalMusicCatalog FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException("Catalog JSON must not be empty.", nameof(json));
            }

            var catalog = JsonUtility.FromJson<LocalMusicCatalog>(json);
            if (catalog == null)
            {
                throw new FormatException("Local music catalog JSON is invalid.");
            }

            catalog.Validate();
            return catalog;
        }

        public LocalMusicSelection Select(
            long musicId,
            MusicDifficulty difficulty)
        {
            if (Musics != null)
            {
                foreach (var music in Musics)
                {
                    if (music == null || music.Id != musicId || music.Lives == null)
                    {
                        continue;
                    }

                    foreach (var live in music.Lives)
                    {
                        if (live != null && live.Difficulty == difficulty)
                        {
                            return new LocalMusicSelection(music, live);
                        }
                    }
                }
            }

            throw new KeyNotFoundException(string.Format(
                "Music {0} does not contain difficulty {1}.",
                musicId,
                difficulty));
        }

        public void Validate()
        {
            var musicIds = new HashSet<long>();
            var liveIds = new HashSet<long>();
            if (Musics == null)
            {
                throw new FormatException("Catalog Musics must not be null.");
            }

            foreach (var music in Musics)
            {
                if (music == null || music.Id <= 0 || string.IsNullOrEmpty(music.Name))
                {
                    throw new FormatException("Every music requires a positive Id and Name.");
                }
                if (!musicIds.Add(music.Id))
                {
                    throw new FormatException("Duplicate music Id: " + music.Id);
                }
                if (music.MusicCoverType != 1 && music.MusicCoverType != 2)
                    throw new FormatException("Music requires a valid MusicCoverType: " + music.Id);
                if (music.VocalVersion <= 0 ||
                    string.IsNullOrEmpty(music.Vocals) ||
                    music.ActorIds == null ||
                    music.Lives == null ||
                    music.Lives.Length == 0)
                {
                    throw new FormatException(
                        "Every music requires vocals, a vocal version, and lives.");
                }

                var difficulties = new HashSet<MusicDifficulty>();
                foreach (var live in music.Lives)
                {
                    if (live == null || live.Id <= 0 || live.Difficulty == MusicDifficulty.None)
                    {
                        throw new FormatException("Every live requires an Id and difficulty.");
                    }
                    if (!liveIds.Add(live.Id))
                    {
                        throw new FormatException("Duplicate live Id: " + live.Id);
                    }
                    if (!difficulties.Add(live.Difficulty))
                    {
                        throw new FormatException(string.Format(
                            "Music {0} contains duplicate difficulty {1}.",
                            music.Id,
                            live.Difficulty));
                    }
                    if (string.IsNullOrEmpty(live.DebugNotationAssetPath) ||
                        string.IsNullOrEmpty(live.DebugMusicConfigAssetPath))
                    {
                        throw new FormatException("Every local live requires both debug asset paths.");
                    }
                }
            }
            if (CharacterBases == null || CharacterBases.Length == 0)
                throw new FormatException(
                    "Catalog CharacterBases must not be empty.");
            var characterIds = new HashSet<long>();
            foreach (var character in CharacterBases)
            {
                if (character == null || character.Id <= 0 ||
                    string.IsNullOrEmpty(character.Name) ||
                    string.IsNullOrEmpty(character.ShortName) ||
                    !characterIds.Add(character.Id))
                    throw new FormatException(
                        "Every character base requires a unique positive Id, Name, and ShortName.");
            }
            foreach (var music in Musics)
            {
                foreach (var actorId in music.ActorIds)
                {
                    if (!characterIds.Contains(actorId))
                        throw new FormatException(
                            $"Music {music.Id} references missing actor {actorId}.");
                }
            }
        }
    }

    public sealed class LocalMusicSelection
    {
        public LocalMusicEntry Music { get; private set; }
        public LocalLiveEntry Live { get; private set; }

        public LocalMusicSelection(
            LocalMusicEntry music,
            LocalLiveEntry live)
        {
            Music = music ?? throw new ArgumentNullException(nameof(music));
            Live = live ?? throw new ArgumentNullException(nameof(live));
        }

        public GamePresenterParameter CreatePresenterParameter(bool isAuto)
        {
            return new GamePresenterParameter
            {
                MusicId = Music.Id,
                AnotherNotationId = Live.AnotherNotationId,
                Difficulty = Live.Difficulty,
                IsAuto = isAuto,
                ProtoNotationUrl = string.Empty,
                ProtoMusicConfigUrl = string.Empty,
                LiveType = LiveType.Normal,
                MusicVideoType = Music.MusicVideoType,
                StartTimingSeconds = 0f,
                IsTutorial = false,
                VocalVersion = Music.VocalVersion,
                DebugNotationAssetPath = Live.DebugNotationAssetPath,
                DebugMusicConfigAssetPath = Live.DebugMusicConfigAssetPath,
            };
        }
    }

    /// <summary>
    /// Recovered single-live subset of GamePresenterParameter. The two proto URLs stay
    /// empty because the recovery project resolves the explicit local asset paths before
    /// invoking gameplay; this is not an Android release factory branch.
    /// </summary>
    [Serializable]
    public sealed class GamePresenterParameter
    {
        public long AnotherNotationId;
        public long MusicId;
        public MusicDifficulty Difficulty;
        public bool IsAuto;
        public string ProtoNotationUrl;
        public string ProtoMusicConfigUrl;
        public LiveType LiveType;
        public MusicVideoType MusicVideoType;
        public float StartTimingSeconds;
        public bool IsTutorial;
        public int VocalVersion;
        public string DebugNotationAssetPath;
        public string DebugMusicConfigAssetPath;

        public GameParameter CreateGameParameter()
        {
            return new GameParameter
            {
                MusicId = MusicId,
                AnotherNotationId = AnotherNotationId,
                Difficulty = Difficulty,
                LiveType = LiveType,
                IsAuto = IsAuto,
                IsTutorial = IsTutorial,
                VocalVersion = VocalVersion,
                MusicVideoType = MusicVideoType,
            };
        }
    }

    /// <summary>
    /// Fields retained when GameSceneInitializer converts GamePresenterParameter into
    /// GameParameter for a normal single live.
    /// </summary>
    [Serializable]
    public sealed class GameParameter
    {
        public long AnotherNotationId;
        public long MusicId;
        public MusicDifficulty Difficulty;
        public LiveType LiveType;
        public bool IsAuto;
        public bool IsTutorial;
        public int VocalVersion;
        public MusicVideoType MusicVideoType;
    }
}
