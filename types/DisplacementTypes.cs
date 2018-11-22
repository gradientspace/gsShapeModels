// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    public interface IVectorDisplacement
    {
        int Count { get; }
        Vector3d GetDisplacementForIndex(int i);
    }


    public class VectorDisplacement : IVectorDisplacement
    {
        DVectorArray3d V;

        public VectorDisplacement()
        {
            V = new DVectorArray3d();
        }

        public VectorDisplacement(byte[] data)
        {
            V = new DVectorArray3d();
            V.vector = new DVector<double>( BufferUtil.ToDouble(data) );
        }

        public int Count {
            get { return V.Count; }
        }

        public void Resize(int maxID) {
            V.Resize(maxID);
        }

        public void Clear() {
            V.Clear();
        }

        public Vector3d this[int i] {
            get { return V[i]; }
            set { V[i] = value; }
        }

        public Vector3d GetDisplacementForIndex(int i) {
            return V[i];
        }

        public void SetToValue(Vector3d value)
        {
            int N = V.Count;
            for (int i = 0; i < N; ++i)
                V[i] = value;
        }

        public void Set(VectorDisplacement d)
        {
            int N = d.V.Count;
            V.Resize(N);
            for (int i = 0; i < N; ++i)
                V[i] = d.V[i];
        }

        public bool IsNonZero(double dMinLen = MathUtil.ZeroTolerancef)
        {
            int N = V.Count;
            double d_sqr = dMinLen * dMinLen;
            for ( int i = 0; i < N; ++i ) {
                if (V[i].LengthSquared > d_sqr)
                    return true;
            }
            return false;
        }

        public byte[] GetBytes()
        {
            return V.vector.GetBytes();
        }

    }

}
