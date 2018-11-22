// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    // [TODO]
    //
    // This is meant to be for implementing simpler mesh operations we might want to tack onto something more complicated
    //


    public abstract class DMeshModifier
    {
        public abstract void Apply(DMesh3 mesh);
    }


    public class DMeshModifierFunction : DMeshModifier
    {
        public Action<DMesh3> Func;

        public override void Apply(DMesh3 mesh) {
            Func(mesh);
        }
    }



    public class DMeshModifierStack : DMeshModifier
    {
        public List<DMeshModifier> Modifiers;

        public DMeshModifierStack()
        {
            Modifiers = new List<DMeshModifier>();
        }

        public void Append(DMeshModifier m)
        {
            Modifiers.Add(m);
        }

        public override void Apply(DMesh3 mesh)
        {
            foreach ( DMeshModifier m in Modifiers ) {
                m.Apply(mesh);
            }
        }

    }
}
