﻿using BigBoxNetflixUI.Service;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unbroken.LaunchBox.Plugins;
using Unbroken.LaunchBox.Plugins.Data;
using System.Data;
using BigBoxNetflixUI.Models;
using System.Speech.Recognition;
using System.Security.Cryptography;
using Unbroken.LaunchBox.Plugins.RetroAchievements;

namespace BigBoxNetflixUI.View
{
    class MainWindowViewModel : INotifyPropertyChanged
    {
        private ListCycle<GameList> listCycle;
        private ListCycle<GameMatch> gameCycle;

        private List<GameList> TempGameLists { get; set; }
        private SpeechRecognitionEngine Recognizer { get; set; }
        private Dictionary<string, List<GameMatch>> GameTitlePhrases;
        private List<IGame> allGames;
        public List<IGame> AllGames
        {
            get { return allGames; }
            set
            {
                if(allGames != value)
                {
                    allGames = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("AllGames"));
                }
            }
        }

        private bool isInitializing;
        public bool IsInitializing
        {
            get { return isInitializing; }
            set
            {
                if (isInitializing != value)
                {
                    isInitializing = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("IsInitializing"));
                }
            }
        }

        private bool isPickingCategory;
        public bool IsPickingCategory
        {
            get { return isPickingCategory; }
            set
            {
                if(isPickingCategory != value)
                {
                    isPickingCategory = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("IsPickingCategory"));
                }
            }
        }

        private bool isDisplayingFeature;
        public bool IsDisplayingFeature
        {
            get { return isDisplayingFeature; }
            set
            {
                if(isDisplayingFeature != value)
                {
                    isDisplayingFeature = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("IsDisplayingFeature"));
                }
            }
        }

        private bool isRecognizing;
        public bool IsRecognizing
        {
            get { return isRecognizing; }
            set
            {
                if (isRecognizing != value)
                {
                    isRecognizing = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("IsRecognizing"));
                }
            }
        }

        private bool isDisplayingResults;
        public bool IsDisplayingResults
        {
            get { return isDisplayingResults; }
            set
            {
                if (isDisplayingResults != value)
                {
                    isDisplayingResults = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("IsDisplayingResults"));
                }
            }
        }

        private bool isDisplayingError;
        public bool IsDisplayingError
        {
            get { return isDisplayingError; }
            set
            {
                if (isDisplayingError != value)
                {
                    isDisplayingError = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("IsDisplayingError"));
                }
            }
        }

        private string errorMessage;
        public string ErrorMessage
        {
            get { return errorMessage; }
            set
            {
                if(errorMessage != value)
                {
                    errorMessage = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("ErrorMessage"));
                }
            }
        }


        private int totalGameCount;
        public int TotalGameCount
        {
            get { return totalGameCount; }
            set
            {
                if (totalGameCount != value)
                {
                    totalGameCount = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("TotalGameCount"));
                }
            }
        }


        private int initializationGameCount;
        public int InitializationGameCount
        {
            get { return initializationGameCount; }
            set
            {
                if (initializationGameCount != value)
                {
                    initializationGameCount = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("InitializationGameCount"));
                }
            }
        }

        private List<GameList> gameLists;
        public List<GameList> GameLists
        {
            get { return gameLists; }
            set
            {
                if(gameLists != value)
                {
                    gameLists = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("GameLists"));
                }
            }
        }

        private void GetGamesByPlatform()
        {
            List<GameList> listOfPlatformGames = new List<GameList>();

            List<IPlatform> platforms = new List<IPlatform>(PluginHelper.DataManager.GetAllPlatforms());
            var orderedPlatforms = platforms.OrderBy(f => f.ReleaseDate);

            foreach (var platform in orderedPlatforms)
            {
                var platformGames = from game in AllGames
                                    where game.Platform.Equals(platform.Name)
                                    select new GameMatch(game, TitleMatchType.None);

                GameList gameList = new GameList();
                gameList.ListDescription = platform.Name;
                gameList.MatchingGames = new List<GameMatch>(platformGames);
                listOfPlatformGames.Add(gameList);
            }
            GameLists = listOfPlatformGames;
            listCycle = new ListCycle<GameList>(GameLists, 3);
            RefreshGameLists();
        }

        public MainWindowViewModel()
        {
            IsInitializing = true;
            InitializeData();
        }

        private void InitializeData()
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += Initialization_LoadData;
            worker.RunWorkerAsync();
        }

        void Initialization_LoadData(object sender, DoWorkEventArgs e)
        {
            IsInitializing = true;

            GameTitlePhrases = new Dictionary<string, List<GameMatch>>();

            AllGames = DataService.GetGames();
            TotalGameCount = AllGames?.Count ?? 0;

            InitializationGameCount = 0;
            foreach (IGame game in AllGames)
            {
                InitializationGameCount += 1;
                GameTitleGrammarBuilder gameTitleGrammarBuilder = new GameTitleGrammarBuilder(game);

                if (!string.IsNullOrWhiteSpace(gameTitleGrammarBuilder.Title))
                {
                    AddGameToVoiceDictionary(gameTitleGrammarBuilder.Title, new GameMatch(game, TitleMatchType.FullTitleMatch));
                }

                if (!string.IsNullOrWhiteSpace(gameTitleGrammarBuilder.MainTitle))
                {
                    AddGameToVoiceDictionary(gameTitleGrammarBuilder.MainTitle, new GameMatch(game, TitleMatchType.MainTitleMatch));
                }

                if (!string.IsNullOrWhiteSpace(gameTitleGrammarBuilder.Subtitle))
                {
                    AddGameToVoiceDictionary(gameTitleGrammarBuilder.Subtitle, new GameMatch(game, TitleMatchType.SubtitleMatch));
                }

                for (int i = 0; i < gameTitleGrammarBuilder.TitleWords.Count; i++)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int j = i; j < gameTitleGrammarBuilder.TitleWords.Count; j++)
                    {
                        sb.Append($"{gameTitleGrammarBuilder.TitleWords[j]} ");
                        if (!GameTitleGrammarBuilder.IsNoiseWord(sb.ToString().Trim()))
                        {
                            AddGameToVoiceDictionary(sb.ToString().Trim(), new GameMatch(game, TitleMatchType.FullTitleContains));
                        }
                    }
                }
            }
            CreateRecognizer();

            // load up results by platform
            GetGamesByPlatform();

            IsInitializing = false;
            IsDisplayingResults = true;
        }

        private bool CreateRecognizer()
        {
            List<string> titleElements = new List<string>(GameTitlePhrases.Keys);

            // add the distinct phrases to the list of choices
            Choices choices = new Choices();
            choices.Add(titleElements.ToArray());

            GrammarBuilder grammarBuilder = new GrammarBuilder();
            grammarBuilder.Append(choices);

            Grammar grammar = new Grammar(grammarBuilder)
            {
                Name = "Game title elements"
            };

            // setup the recognizer
            Recognizer = new SpeechRecognitionEngine();
            Recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(5.0);
            Recognizer.RecognizeCompleted += new EventHandler<RecognizeCompletedEventArgs>(RecognizeCompleted);
            Recognizer.LoadGrammarAsync(grammar);
            Recognizer.SpeechHypothesized += new EventHandler<SpeechHypothesizedEventArgs>(SpeechHypothesized);
            Recognizer.SetInputToDefaultAudioDevice();
            Recognizer.RecognizeAsyncCancel();
            return (true);
        }

        void SpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            // ignore noise words 
            if (!GameTitleGrammarBuilder.IsNoiseWord(e.Result.Text))
            {
                TempGameLists.Add(new GameList
                {
                    ListDescription = e.Result.Text,
                    Confidence = e.Result.Confidence
                });
            }
        }

        void RecognizeCompleted(object sender, RecognizeCompletedEventArgs e)
        {
            IsRecognizing = false;
            List<GameList> voiceRecognitionResults = new List<GameList>();

            if (e?.Error != null)
            {
                if (Recognizer != null)
                {
                    Recognizer.RecognizeAsyncCancel();
                }

                IsDisplayingError = true;
                ErrorMessage = e.Error.Message;
                return;
            }

            if (e?.InitialSilenceTimeout == true || e?.BabbleTimeout == true)
            {
                if (Recognizer != null)
                {
                    Recognizer.RecognizeAsyncCancel();
                }

                IsDisplayingError = true;
                ErrorMessage = "Voice recognition could not hear anything, please try again";
                return;
            }

            if (TempGameLists?.Count() > 0)
            {
                // in case the same phrase was recognized multiple times, group by phrase and keep only the max confidence
                var distinctGameLists = TempGameLists
                    .GroupBy(s => s.ListDescription)
                    .Select(s => new GameList { ListDescription = s.Key, Confidence = s.Max(m => m.Confidence) }).ToList();

                foreach(var gameList in distinctGameLists)
                {
                    List<GameMatch> matches;
                    if(GameTitlePhrases.TryGetValue(gameList.ListDescription, out matches))
                    {
                        gameList.MatchingGames = matches;
                        voiceRecognitionResults.Add(gameList);
                    }
                }
            }

            GameLists = voiceRecognitionResults;
            IsDisplayingResults = true;
        }

        private void AddGameToVoiceDictionary(string phrase, GameMatch gameMatch)
        {
            if (GameTitleGrammarBuilder.IsNoiseWord(phrase))
            {
                return;
            }

            // add the phrase if it's not in the dictionary
            if (!GameTitlePhrases.ContainsKey(phrase))
            {
                GameTitlePhrases.Add(phrase, new List<GameMatch>());
            }

            // add the game if it's not already in the collection of matching games
            if (!GameTitlePhrases[phrase].Contains(gameMatch))
            {
                GameTitlePhrases[phrase].Add(gameMatch);
            }
        }

        public void DoRecognize()
        {
            if (IsRecognizing)
                return;

            if (IsInitializing)
                return;

            IsRecognizing = true;
            IsDisplayingResults = false;
            IsDisplayingError = false;

            // reset the results and the temporary results
            TempGameLists = new List<GameList>();

            // kick off voice recognition 
            Recognizer.RecognizeAsync(RecognizeMode.Single);
        }

        private void RefreshGameLists()
        {
            CurrentGameList = listCycle.GetItem(0);
            NextGameList = listCycle.GetItem(1);

            gameCycle = new ListCycle<GameMatch>(CurrentGameList.MatchingGames, 15);
        }

        private void CycleListBackward()
        {
            listCycle.CycleBackward();
            RefreshGameLists();
        }

        private void CycleListForward()
        {
            listCycle.CycleForward();
            RefreshGameLists();
        }

        public void DoUp(bool held)
        {
            if(isDisplayingResults)
            {
                // todo: if displaying first list - change to featured game

                // cycle to prior list
                CycleListBackward();
            }

            if (isPickingCategory)
            {
                // todo: cycle up through categories 
            }
        }

        public void DoDown(bool held)
        {
            if(isDisplayingResults)
            {
                // cycle to next list
                CycleListForward();
            }

            if (isPickingCategory)
            {
                // todo: change down through categories
            }

            if(isDisplayingFeature)
            {
                // todo: change to displaying first result
            }
        }

        public void DoLeft(bool held)
        {
            if(isDisplayingResults)
            {
                // todo: display category category options
                // todo: cycle left to prior game if other than 1st game
                CurrentGameList.CycleBackward();
            }

            if (IsDisplayingFeature)
            {
                // todo: expand category options
            }
        }

        public void DoRight(bool held)
        {
            if(isDisplayingResults)
            {
                CurrentGameList.CycleForward();
                // todo: cycle right to next game
            }

            if (isPickingCategory)
            {
                // todo: collapse category options
            }
        }

        public void DoPageUp()
        {
            // do voice recognition
            DoRecognize();
        }

        public void DoPageDown()
        {
            // do voice recognition
            DoRecognize();
        }

        public void DoEnter()
        {
            if(isPickingCategory)
            {
                // todo: update for selected category
                // todo: collapse category options
            }

            if (isDisplayingFeature)
            {
                // todo: start featured game
            }

            if(isDisplayingResults)
            {
                // todo: start selected game
            }
        }

        public void DoEscape()
        {
            // todo: TBD - maybe nothing - maybe go back to prior setting
        }

        private GameList currentGameList;
        public GameList CurrentGameList
        {
            get { return currentGameList; }
            set
            {
                if (currentGameList != value)
                {
                    currentGameList = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("CurrentGameList"));
                }
            }
        }

        private GameList nextGameList;
        public GameList NextGameList
        {
            get { return nextGameList; }
            set
            {
                if (nextGameList != value)
                {
                    nextGameList = value;
                    PropertyChanged(this, new PropertyChangedEventArgs("NextGameList"));
                }
            }
        }

        public Uri VoiceRecognitionGif
        {
            get
            {
                return new Uri("pack://application:,,,/BigBoxNetflixUI;component/resources/VoiceRecognitionGif.gif");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged = delegate { };
    }
}