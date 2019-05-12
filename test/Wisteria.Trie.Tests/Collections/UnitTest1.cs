// Copyright © FUJIWARA, Yusuke 
// This file is licensed to you under the Apache 2 license.
// See the LICENSE file in the project root for more information.

// #nullable enabled

using System;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Wisteria.Collections
{
    [TestFixture]
    public class TrieTest
    {
        [Test]
        public void Basic()
        {
            var target = new Trie<string>();
            Assert.That(target.TryAdd(Encoding.UTF8.GetBytes("ABC"), "ABC"), Is.True);
            Assert.That(target.TryAdd(Encoding.UTF8.GetBytes("ABE"), "ABE"), Is.True);
            Assert.That(target.TryAdd(Encoding.UTF8.GetBytes("AFG"), "AFG"), Is.True);
            Assert.That(target.TryAdd(Encoding.UTF8.GetBytes("HIJ"), "HIJ"), Is.True);

            Assert.That(target.Count, Is.EqualTo(4));

            Assert.That(
                target.Select(x => x.Value).ToArray(),
                Is.EqualTo(new [] {"ABC", "ABE", "AFG", "HIJ"})
            );
        }

		// TODO: simple perf test for netapp3.0 Dictionary tuned with Ben
    }
}
