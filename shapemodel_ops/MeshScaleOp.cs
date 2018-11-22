// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class MeshScaleOp : BaseDMeshSourceOp
    {
        DMesh3 ScaledMesh;

        public bool CopyInput = true;

        double scale_factor = 1.0;
        public double ScaleFactor {
            get { return scale_factor; }
            set { scale_factor = value; base.invalidate(); }
        }


        Vector3d scale_origin = Vector3d.Zero;
        public Vector3d ScaleOrigin {
            get { return scale_origin; }
            set { scale_origin = value; base.invalidate(); }
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


        protected virtual void on_input_modified(ModelingOperator op) {
            base.invalidate();
        }


        public virtual void Update()
        {
            base.begin_update();

            if (MeshSource == null)
                throw new Exception("MeshScaleOp: must set valid MeshSource to compute!");

            DMesh3 mesh = (CopyInput) ? new DMesh3(MeshSource.GetDMeshUnsafe()) : MeshSource.ExtractDMesh();

            if ( ScaleFactor == 1.0 ) {
                ScaledMesh = mesh;
                base.complete_update();
                return;
            }

            gParallel.ForEach(mesh.VertexIndices(), (vid) => {
                Vector3d v = mesh.GetVertex(vid);
                v -= scale_origin;
                v *= scale_factor;
                v += scale_origin;
                mesh.SetVertex(vid, v);
            });

            ScaledMesh = mesh;
            base.complete_update();
        }


        /*
         * IMeshSourceOp / DMeshSourceOp interface
         */

        public override IMesh GetIMesh() {
            if (base.requires_update())
                Update();
            return ScaledMesh;
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
            var result = ScaledMesh;
            ScaledMesh = null;
            base.result_consumed();
            return result;
        }

    }
}
