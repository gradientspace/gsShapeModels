// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using g3;

namespace gs
{

    public class SetVertexColorModififer : DMeshModifier
    {
        public Colorf Color = Colorf.White;

        public override void Apply(DMesh3 mesh)
        {
            Vector3f c = (Vector3f)Color;
            gParallel.ForEach(mesh.VertexIndices(), (vid) => {
                mesh.SetVertexColor(vid, c);
            });
        }

    }


}
