// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class MeshVertexDisplacementOp : BaseDMeshSourceOp
    {
        DMesh3 DisplacedMesh;

        double heat_map_max_dist = 1.0;
        public double HeatMapMaxDistance {
            get { return heat_map_max_dist; }
            set { heat_map_max_dist = value; base.invalidate(); }
        }


        bool enable_heat_map = true;
        public bool EnableHeatMap {
            get { return enable_heat_map; }
            set { enable_heat_map = value; base.invalidate(); }
        }


        DMeshSourceOp mesh_source;
        public DMeshSourceOp MeshSource {
            get { return mesh_source; }
            set {
                if (mesh_source != null)
                    mesh_source.OperatorModified -= on_input_modified;
                mesh_source = value;
                if (mesh_source != null)
                    mesh_source.OperatorModified += on_input_modified;
                base.invalidate();
            }
        }


        IVectorDisplacementSourceOp displace_source;
        public IVectorDisplacementSourceOp DisplacementSource {
            get { return displace_source; }
            set {
                if (displace_source != null)
                    displace_source.OperatorModified -= on_input_modified;
                displace_source = value;
                if (displace_source != null)
                    displace_source.OperatorModified += on_input_modified;
                base.invalidate();
            }
        }

        protected virtual void on_input_modified(ModelingOperator op) {
            base.invalidate();
        }




        public virtual void Update()
        {
            base.begin_update();

            if (MeshSource == null)
                throw new Exception("MeshVertexDisplacementOp: must set valid MeshSource to compute!");
            if (DisplacementSource == null)
                throw new Exception("MeshVertexDisplacementOp: must set valid DisplacementSource to compute!");

            DMesh3 meshIn = MeshSource.GetDMeshUnsafe();
            IVectorDisplacement displace = DisplacementSource.GetDisplacement();
            if ( displace.Count != 0 && displace.Count != meshIn.MaxVertexID )
                throw new Exception("MeshVertexDisplacementOp: inconsistent counts " + displace.Count.ToString() + " != " + meshIn.MaxVertexID.ToString());

            DMesh3 mesh = new DMesh3(meshIn, MeshHints.None);

            //if (!mesh.HasVertexNormals)
            //    MeshNormals.QuickCompute(mesh);
            if (displace.Count > 0) {

                gParallel.ForEach(mesh.VertexIndices(), (vid) => {
                    Vector3d dv = displace.GetDisplacementForIndex(vid);

                    //Vector3f n = mesh.GetVertexNormal(vid);
                    Vector3d v = mesh.GetVertex(vid);

                    v += dv;

                    mesh.SetVertex(vid, v);
                });

                if (enable_heat_map) {
                    // compute max displace len
                    ColorMap map = new ColorMap();
                    map.AddPoint(0, Colorf.CornflowerBlue);
                    float d = (float)HeatMapMaxDistance;
                    map.AddPoint(d, Colorf.Orange);
                    map.AddPoint(2 * d, Colorf.VideoYellow);
                    map.AddPoint(4 * d, Colorf.VideoRed);
                    map.AddPoint(-d, Colorf.VideoMagenta);

                    float max_displace = d;
                    gParallel.ForEach(mesh.VertexIndices(), (vid) => {
                        Vector3f dv = (Vector3f)displace.GetDisplacementForIndex(vid);

                        Vector3f n = mesh.GetVertexNormal(vid);
                        float sign = n.Dot(dv) > 0 ? 1 : -1;

                        Colorf c = map.Linear(dv.Length * sign);

                        Colorf existing_c = mesh.GetVertexColor(vid);
                        float preserve__max = max_displace / 2;
                        float t = MathUtil.Clamp(dv.Length / preserve__max, 0.0f, 1.0f);
                        c = (1.0f - t) * existing_c + (t) * c;

                        mesh.SetVertexColor(vid, c);

                        //float t = MathUtil.Clamp(dv.Length / max_displace, -1.0f, 1.0f);
                        //mesh.SetVertexColor(vid, t * Colorf.Orange);
                    });
                }
            }

            MeshNormals.QuickCompute(mesh);

            DisplacedMesh = mesh;
            base.complete_update();
        }


        /*
         * IMeshSourceOp / DMeshSourceOp interface
         */

        public override IMesh GetIMesh() {
            if (base.requires_update())
                Update();
            return DisplacedMesh;
        }

        public override DMesh3 GetDMeshUnsafe() {
            return (DMesh3)GetIMesh();
        }

        public override bool HasSpatial {
            get { return false; }
        }
        public override ISpatial GetSpatial() {
            return null;
        }

        public override DMesh3 ExtractDMesh()
        {
            Update();
            var result = DisplacedMesh;
            DisplacedMesh = null;
            base.result_consumed();
            return result;
        }

    }
}
