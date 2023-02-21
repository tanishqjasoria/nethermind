using Nethermind.Core.Extensions;
using Nethermind.Field.Montgomery.FrEElement;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Polynomial;

namespace Nethermind.Verkle.Proofs.Test
{
    public class MultiProofTests
    {
        private readonly FrE[] _poly =
        {
            FrE.SetElement(1), FrE.SetElement(2), FrE.SetElement(3), FrE.SetElement(4), FrE.SetElement(5), FrE.SetElement(6), FrE.SetElement(7), FrE.SetElement(8), FrE.SetElement(9), FrE.SetElement(10), FrE.SetElement(11), FrE.SetElement(12), FrE.SetElement(13),
            FrE.SetElement(14), FrE.SetElement(15), FrE.SetElement(16), FrE.SetElement(17), FrE.SetElement(18), FrE.SetElement(19), FrE.SetElement(20), FrE.SetElement(21), FrE.SetElement(22), FrE.SetElement(23), FrE.SetElement(24), FrE.SetElement(25), FrE.SetElement(26),
            FrE.SetElement(27), FrE.SetElement(28), FrE.SetElement(29), FrE.SetElement(30), FrE.SetElement(31), FrE.SetElement(32)
        };

        [Test]
        public void TestBasicMultiProof()
        {
            List<FrE> polyEvalA = new List<FrE>();
            List<FrE> polyEvalB = new List<FrE>();

            for (int i = 0; i < 8; i++)
            {
                polyEvalA.AddRange(_poly);
                polyEvalB.AddRange(_poly.Reverse());
            }
            CRS crs = CRS.Instance;
            Banderwagon cA = crs.Commit(polyEvalA.ToArray());
            Banderwagon cB = crs.Commit(polyEvalB.ToArray());

            FrE[] zs =
            {
                FrE.Zero, FrE.Zero
            };
            FrE[] ys =
            {
                FrE.SetElement(1), FrE.SetElement(32)
            };
            FrE[][] fs =
            {
                polyEvalA.ToArray(), polyEvalB.ToArray()
            };

            Banderwagon[] cs =
            {
                cA, cB
            };


            VerkleProverQuery queryA = new VerkleProverQuery(new LagrangeBasis(fs[0]), cs[0], zs[0], ys[0]);
            VerkleProverQuery queryB = new VerkleProverQuery(new LagrangeBasis(fs[1]), cs[1], zs[1], ys[1]);

            MultiProof multiproof = new MultiProof(crs, PreComputeWeights.Init());

            Transcript proverTranscript = new Transcript("test");
            VerkleProverQuery[] queries =
            {
                queryA, queryB
            };
            VerkleProofStruct proof = multiproof.MakeMultiProof(proverTranscript, new List<VerkleProverQuery>(queries));
            FrE pChallenge = proverTranscript.ChallengeScalar("state");

            Assert.IsTrue(Convert.ToHexString(pChallenge.ToBytes()).ToLower()
                .SequenceEqual("eee8a80357ff74b766eba39db90797d022e8d6dee426ded71234241be504d519"));

            Transcript verifierTranscript = new Transcript("test");
            VerkleVerifierQuery queryAx = new VerkleVerifierQuery(cs[0], zs[0], ys[0]);
            VerkleVerifierQuery queryBx = new VerkleVerifierQuery(cs[1], zs[1], ys[1]);

            VerkleVerifierQuery[] queriesX =
            {
                queryAx, queryBx
            };
            bool ok = multiproof.CheckMultiProof(verifierTranscript, queriesX, proof);
            Assert.That(ok, Is.True);

            FrE vChallenge = verifierTranscript.ChallengeScalar("state");
            Assert.That(vChallenge, Is.EqualTo(pChallenge));
        }

        [Test]
        public void TestMultiProofConsistency()
        {
            FrE[] polyA = TestPoly256(new ulong[]
                {
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
                }
            );

            FrE[] polyB = TestPoly256(new ulong[]
                {
                    32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1,
                    32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1,
                    32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1,
                    32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1,
                    32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1,
                    32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1,
                    32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1,
                    32, 31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1
                }
            );

            Transcript proverTranscript = new Transcript("test");

            CRS cfg = CRS.Instance;

            Banderwagon commA = cfg.Commit(polyA);
            Banderwagon commB = cfg.Commit(polyB);

            var one = FrE.One;
            var thirtyTwo = FrE.SetElement(32);

            VerkleProverQuery queryA = new VerkleProverQuery(new LagrangeBasis(polyA), commA, FrE.SetElement(0), one);
            VerkleProverQuery queryB = new VerkleProverQuery(new LagrangeBasis(polyB), commB, FrE.SetElement(0), thirtyTwo);

            MultiProof multiproof = new MultiProof(cfg, PreComputeWeights.Init());
            List<VerkleProverQuery> queries = new List<VerkleProverQuery>
            {
                queryA,
                queryB
            };

            VerkleProofStruct proof = multiproof.MakeMultiProof(proverTranscript, queries);
            FrE pChallenge = proverTranscript.ChallengeScalar("state");

            string pChallengeExcepted = "eee8a80357ff74b766eba39db90797d022e8d6dee426ded71234241be504d519";
            Assert.IsTrue(pChallenge.ToBytes().ToHexString().SequenceEqual(pChallengeExcepted));

            string expectedProof =
                "4f53588244efaf07a370ee3f9c467f933eed360d4fbf7a19dfc8bc49b67df4711bf1d0a720717cd6a8c75f1a668cb7cbdd63b48c676b89a7aee4298e71bd7" +
                "f4013d7657146aa9736817da47051ed6a45fc7b5a61d00eb23e5df82a7f285cc10e67d444e91618465ca68d8ae4f2c916d1942201b7e2aae491ef0f809867" +
                "d00e83468fb7f9af9b42ede76c1e90d89dd789ff22eb09e8b1d062d8a58b6f88b3cbe80136fc68331178cd45a1df9496ded092d976911b5244b85bc3de41e" +
                "844ec194256b39aeee4ea55538a36139211e9910ad6b7a74e75d45b869d0a67aa4bf600930a5f760dfb8e4df9938d1f47b743d71c78ba8585e3b80aba26d2" +
                "4b1f50b36fa1458e79d54c05f58049245392bc3e2b5c5f9a1b99d43ed112ca82b201fb143d401741713188e47f1d6682b0bf496a5d4182836121efff0fd3b" +
                "030fc6bfb5e21d6314a200963fe75cb856d444a813426b2084dfdc49dca2e649cb9da8bcb47859a4c629e97898e3547c591e39764110a224150d579c33fb7" +
                "4fa5eb96427036899c04154feab5344873d36a53a5baefd78c132be419f3f3a8dd8f60f72eb78dd5f43c53226f5ceb68947da3e19a750d760fb31fa8d4c7f" +
                "53bfef11c4b89158aa56b1f4395430e16a3128f88e234ce1df7ef865f2d2c4975e8c82225f578310c31fd41d265fd530cbfa2b8895b228a510b806c31dff3" +
                "b1fa5c08bffad443d567ed0e628febdd22775776e0cc9cebcaea9c6df9279a5d91dd0ee5e7a0434e989a160005321c97026cb559f71db23360105460d959b" +
                "cdf74bee22c4ad8805a1d497507";

            Assert.IsTrue(proof.Encode().ToHexString().SequenceEqual(expectedProof));
        }

        private static FrE[] TestPoly256(ulong[] polynomial)
        {
            FrE[] poly = new FrE[256];
            for (int i = 0; i < 256; i++)
            {
                poly[i] = FrE.Zero;
            }

            for (int i = 0; i < polynomial.Length; i++)
            {
                poly[i] = FrE.SetElement(polynomial[i]);
            }

            return poly;
        }
    }
}
