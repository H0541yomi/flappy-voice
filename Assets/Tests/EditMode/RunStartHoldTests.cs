using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FlappyVoice.Tests
{
    // A run starts on a sung note, so no panel can stop one by covering the screen. The camera ask
    // goes up in Attract with the microphone already live, and these pin the only thing that keeps
    // the player's next note from starting a round behind that parchment.
    public class RunStartHoldTests
    {
        private GameConfig config;
        private GameObject go;
        private GameStateManager stateManager;

        [SetUp]
        public void SetUp()
        {
            config = GameConfig.CreateDefault();
            go = new GameObject("state");
            stateManager = go.AddComponent<GameStateManager>();
            stateManager.Configure(config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void HeldAttractRefusesToStart()
        {
            stateManager.SetRunStartHeld(true);
            stateManager.StartRun();

            Assert.That(stateManager.State, Is.EqualTo(GameState.Attract));
        }

        [Test]
        public void ReleasingTheHoldLetsTheNextNoteStart()
        {
            stateManager.SetRunStartHeld(true);
            stateManager.StartRun();
            stateManager.SetRunStartHeld(false);
            stateManager.StartRun();

            Assert.That(stateManager.State, Is.EqualTo(GameState.Playing));
        }
    }
}
