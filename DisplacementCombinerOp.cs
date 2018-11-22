// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class DisplacementCombinerOp : BaseSingleOutputModelingOperator, IVectorDisplacementSourceOp
    {
        ShapeModelingOpList<IVectorDisplacementSourceOp> Displacements;
        VectorDisplacement CombinedDisplacement;


        public DisplacementCombinerOp()
        {
            Displacements = new ShapeModelingOpList<IVectorDisplacementSourceOp>();
            Displacements.CollectionModified += on_collection_modified;
            Displacements.CollectionItemModified += on_child_modified;
            CombinedDisplacement = new VectorDisplacement();
        }


        public void Append(IVectorDisplacementSourceOp op)
        {
            Displacements.Append(op);
        }
        public void Remove(IVectorDisplacementSourceOp op)
        {
            Displacements.Remove(op);
        }


        public IReadOnlyList<IVectorDisplacementSourceOp> DisplacementOps {
            get { return Displacements.Operators; }
        }


        void on_collection_modified(ModelingOpCollection collection)
        {
            base.invalidate();
        }
        void on_child_modified(ModelingOpCollection collection, ModelingOperator op)
        {
            base.invalidate();
        }



        public virtual void Update()
        {
            base.begin_update();

            int Nd = Displacements.Count;
            if ( Nd == 0 ) {
                for ( int i = 0; i < CombinedDisplacement.Count; ++i ) {
                    CombinedDisplacement[i] = Vector3d.Zero;
                }
                base.complete_update();
                return;
            }


            IVectorDisplacement[] children = new IVectorDisplacement[Nd];
            for ( int i = 0; i < Nd; ++i ) {
                children[i] = Displacements[i].GetDisplacement();
                if (children[i].Count != children[0].Count)
                    throw new Exception("DisplacementCombinerOp.Update: child " + i.ToString() + " Count inconsistent: " + children[i].Count + " != " + children[0].Count);
            }

            int Nv = children[0].Count;
            CombinedDisplacement.Resize(Nv);

            for ( int vi = 0; vi < Nv; ++vi ) {

                Vector3d sum = Vector3d.Zero;
                for (int di = 0; di < Nd; ++di)
                    sum += children[di].GetDisplacementForIndex(vi);

                CombinedDisplacement[vi] = sum;
            }

            base.complete_update();
        }



        public IVectorDisplacement GetDisplacement() {
            if (base.requires_update())
                Update();
            return CombinedDisplacement;
        }

    }
}
