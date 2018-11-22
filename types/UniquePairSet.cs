// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;


namespace gs
{
    public class UniquePairSet<T1,T2> : IEnumerable<Tuple<T1,T2>>
    {
        Dictionary<T1, T2> AtoB;
        Dictionary<T2, T1> BtoA;

        public UniquePairSet()
        {
            AtoB = new Dictionary<T1, T2>();
            BtoA = new Dictionary<T2, T1>();
        }


        public void Add(T1 a, T2 b)
        {
            if (AtoB.ContainsKey(a) || BtoA.ContainsKey(b))
                throw new Exception("UniquePairSet.Add: either first or second object is already associated");

            AtoB[a] = b;
            BtoA[b] = a;
        }


        public void Remove(T1 a)
        {
            if (!AtoB.ContainsKey(a))
                throw new Exception("UniquePairSet.Remove: value does not exist");
            T2 b = AtoB[a];
            AtoB.Remove(a);
            BtoA.Remove(b);
        }
        public void Remove(T2 b)
        {
            if (!BtoA.ContainsKey(b))
                throw new Exception("UniquePairSet.Remove: value does not exist");
            T1 a = BtoA[b];
            AtoB.Remove(a);
            BtoA.Remove(b);
        }



        public T2 Find(T1 a)
        {
            T2 b;
            bool found = AtoB.TryGetValue(a, out b);
            if (!found)
                return default(T2);
            return b;
        }
        public T1 Find(T2 b)
        {
            T1 a;
            bool found = BtoA.TryGetValue(b, out a);
            if (!found)
                return default(T1);
            return a;
        }



        public IEnumerator<Tuple<T1, T2>> GetEnumerator() {
            foreach ( var pair in AtoB ) {
                yield return new Tuple<T1, T2>(pair.Key, pair.Value);
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }


        public IEnumerable<T1> FirstTypeItr()
        {
            return AtoB.Keys;
        }
        public IEnumerable<T2> SecondTypeItr()
        {
            return BtoA.Keys;
        }

    }
}
