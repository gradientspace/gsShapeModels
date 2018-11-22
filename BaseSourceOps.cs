// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public abstract class BaseDMeshSourceOp : BaseSingleOutputModelingOperator, DMeshSourceOp
    {
        /*
         * IMeshSourceOp / DMeshSourceOp interface you have to implement
         */
        public abstract IMesh GetIMesh();
        public abstract DMesh3 GetDMeshUnsafe();
        public abstract bool HasSpatial { get; }
        public abstract ISpatial GetSpatial();
        public abstract DMesh3 ExtractDMesh();


        DMeshModifierStack Modifiers;

        public BaseDMeshSourceOp()
        {
            Modifiers = new DMeshModifierStack();
        }


        public virtual void AppendModifier(DMeshModifier modifier)
        {
            Modifiers.Append(modifier);
        }


        public virtual void ApplyModifiers(DMesh3 mesh)
        {
            Modifiers.Apply(mesh);
        }



        protected virtual DMesh3 make_failure_output(DMesh3 lastOKMesh, bool bCopy = true)
        {
            return DMeshOpUtil.MakeFailureOutput(lastOKMesh, bCopy);
        }



    }
}
