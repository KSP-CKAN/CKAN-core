using System;
using System.Collections.Generic;
using CKAN;
using NUnit.Framework;
using Tests;

namespace CKANTests
{
    [TestFixture]
    public class Module
    {
        private RandomModuleGenerator generator;

        [SetUp]
        public void Setup()
        {
            generator = new RandomModuleGenerator(new Random(1492));
        }

        [Test]
        public void CompatibleWith()
        {
            CkanModule module = CkanModule.FromJson(TestData.kOS_014());

            Assert.IsTrue(module.IsCompatibleKSP("0.24.2"));
        }

        [Test]
        public void StandardName()
        {
            CkanModule module = CkanModule.FromJson(TestData.kOS_014());

            Assert.AreEqual(module.StandardName(), "kOS-0.14.zip");
        }

        [Test]
        public void MetaData()
        {
            CkanModule module = CkanModule.FromJson (TestData.kOS_014 ());

            // TODO: Test all the metadata here!
            Assert.AreEqual("https://github.com/KSP-KOS/KOS/issues", module.resources.bugtracker.ToString());
        }

        [Test]
        public void FilterRead()
        {
            CkanModule module = CkanModule.FromJson(TestData.DogeCoinFlag_101());

            // Assert known things about this mod.
            Assert.IsNotNull(module.install[0].filter);
            Assert.IsNotNull(module.install[0].filter_regexp);

            Assert.AreEqual(2, module.install[0].filter.Count);
        }

        [Test]
        public void SpecCompareAssumptions()
        {
            // These are checks to make sure our assumptions regarding
            // spec versions hold.

            // The *old* CKAN spec had a version number of "1".
            // It should be accepted by any client with an old version number,
            // as well as any with a new version number.
            var old_spec = new CKAN.Version("1");
            var old_dev = new CKAN.Version("v0.23");
            var new_dev = new CKAN.Version("v1.2.3");

            Assert.IsTrue(old_dev.IsGreaterThan(old_spec));
            Assert.IsTrue(new_dev.IsGreaterThan(old_spec));

            // The new spec requires a minimum number (v1.2, v1.4)
            // Make sure our assumptions here hold, too.

            var readable_spec = new CKAN.Version("v1.2");
            var unreadable_spec = new CKAN.Version("v1.4");

            Assert.IsTrue(new_dev.IsGreaterThan(readable_spec));
            Assert.IsFalse(new_dev.IsGreaterThan(unreadable_spec));
        }

        [Test]
        public void IsSpecSupported()
        {
            if (CKAN.Meta.ReleaseNumber() == null)
            {
                Assert.Inconclusive("Dev build");
            }

            // We should always support old versions, and the classic '1' version.
            Assert.IsTrue(CkanModule.IsSpecSupported(new CKAN.Version("1")));
            Assert.IsTrue(CkanModule.IsSpecSupported(new CKAN.Version("v0.02")));

            // We shouldn't support this far-in-the-future version.
            // NB: V2K bug!!!
            Assert.IsFalse(CkanModule.IsSpecSupported(new CKAN.Version("v2000.99.99")));
        }

        [Test]
        public void DottedSpecsSupported()
        {
            // We should support both two and three number dotted specs, on both
            // tagged and dev releases.

            Assert.IsTrue(CkanModule.IsSpecSupported(new CKAN.Version("v1.1")));
            Assert.IsTrue(CkanModule.IsSpecSupported(new CKAN.Version("v1.0.2")));
        }

        [Test]
        public void FutureModule()
        {
            if (CKAN.Meta.ReleaseNumber() == null)
            {
                Assert.Inconclusive("Dev build");
            }

            // Modules form the future are unsupported.

            Assert.Throws<UnsupportedKraken>(delegate
            {
                CkanModule.FromJson(TestData.FutureMetaData());
            });

        }
        //without provides
        [Test]
        [Category("Version")]
        [TestCase("1.0", null, null, null, Result = true)]
        [TestCase("1.0", "0.5", null, null, Result = false)]
        [TestCase("1.0", "1.0", null, null, Result = true)]
        [TestCase("1.0", "2.0", null, null, Result = false)]
        [TestCase("1.0", null, "0.5", null, Result = true)]
        [TestCase("1.0", null, "1.0", null, Result = true)]
        [TestCase("1.0", null, "2.0", null, Result = false)]
        [TestCase("1.0", null, null, "0.5", Result = false)]
        [TestCase("1.0", null, null, "1.0", Result = true)]
        [TestCase("1.0", null, null, "2.0", Result = true)]
        [TestCase("2.0", null, "0.5", "1.0", Result = false)]
        [TestCase("2.0", null, "0.5", "2.0", Result = true)]
        [TestCase("2.0", null, "0.5", "3.0", Result = true)]
        [TestCase("2.0", null, "2.0", "2.0", Result = true)]
        [TestCase("2.0", null, "2.0", "3.0", Result = true)]
        [TestCase("2.0", null, "3.0", "4.0", Result = false)]
        public bool ConflictsWith(string ver, string conf, string conf_min, string conf_max)
        {
            var mod_a = generator.GeneratorRandomModule(version: new CKAN.Version(ver));
            var mod_b = generator.GeneratorRandomModule(conflicts: new List<RelationshipDescriptor>
            {
                new RelationshipDescriptor {name=mod_a.identifier, version=conf, min_version=conf_min, max_version=conf_max}
            });

            bool direct = mod_a.ConflictsWith(mod_b);
            Assert.AreEqual(direct, mod_b.ConflictsWith(mod_a));

            return direct;
        }
    }
}