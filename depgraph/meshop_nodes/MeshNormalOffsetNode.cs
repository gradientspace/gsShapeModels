using System;
using System.Collections.Generic;
using g3;

namespace gs
{
    public class MeshNormalOffsetNode : DGCachingNode
    {
        DMesh3 OffsetMesh;

        public MeshNormalOffsetNode()
        {
            Inputs.Add(new DMesh3InPort());
            Inputs.Add(new DoubleInPort());

            Outputs.Add(new ExternalDMesh3OutPort() {
                MeshSourceF = (args) => { return GetOffsetMesh(args); },
                TimestampSourceF = () => { return InputsTimestamp; }
            });

            OffsetMesh = new DMesh3();
        }


        protected virtual DMesh3 GetOffsetMesh(DGArguments args)
        {
            RunRecompute(args);
            return OffsetMesh;
        }


        protected override void Recompute(DGArguments args)
        {
            OffsetMesh.Copy( CachedValue<DMesh3>(0, args) );
            double dist = CachedValue<double>(1, args);

            if (! OffsetMesh.HasVertexNormals ) {
                MeshNormals.QuickCompute(OffsetMesh);
            }

            foreach ( int vid in OffsetMesh.VertexIndices() ) {
                Vector3d v = OffsetMesh.GetVertex(vid);
                Vector3d n = OffsetMesh.GetVertexNormal(vid);
                v += dist * n;
                OffsetMesh.SetVertex(vid, v);
            }
        }

    }
}
