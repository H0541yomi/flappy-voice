using FlappyVoice.Config;
using FlappyVoice.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FlappyVoice.Tests
{
    public class LivesTests
    {
        private GameConfig config;
        private GameObject go;
        private LivesManager lives;

        [SetUp]
        public void SetUp()
        {
            config = GameConfig.CreateDefault();
            go = new GameObject("lives");
            lives = go.AddComponent<LivesManager>();
            lives.Configure(config, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void StartsWithThree()
        {
            Assert.That(lives.MaxLives, Is.EqualTo(3));
            Assert.That(lives.Lives, Is.EqualTo(3));
        }

        // Three lives means three hits: the first two are survivable, the third ends the run.
        [Test]
        public void ThirdHitEndsTheRun()
        {
            Assert.That(lives.TryConsumeLife(), Is.True, "first hit should be survivable");
            Assert.That(lives.Lives, Is.EqualTo(2));

            Assert.That(lives.TryConsumeLife(), Is.True, "second hit should be survivable");
            Assert.That(lives.Lives, Is.EqualTo(1));

            Assert.That(lives.TryConsumeLife(), Is.False, "third hit should end the run");
            Assert.That(lives.Lives, Is.EqualTo(0));
        }

        [Test]
        public void CannotGoNegativeOrReviveByHittingAgain()
        {
            lives.TryConsumeLife();
            lives.TryConsumeLife();
            lives.TryConsumeLife();

            Assert.That(lives.TryConsumeLife(), Is.False);
            Assert.That(lives.Lives, Is.EqualTo(0));
        }

        [Test]
        public void ResetRestoresFullLives()
        {
            lives.TryConsumeLife();
            lives.ResetLives();
            Assert.That(lives.Lives, Is.EqualTo(3));
        }

        [Test]
        public void ChangesAreAnnouncedForTheHud()
        {
            int seen = -1;
            lives.OnLivesChanged += v => seen = v;
            lives.TryConsumeLife();
            Assert.That(seen, Is.EqualTo(2));
        }
    }
}
