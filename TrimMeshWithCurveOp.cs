// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class TrimMeshWithCurveOp : BaseDMeshSourceOp
    {
        DMesh3 TrimmedMesh;

        IMeshSourceOp mesh_source;
        public IMeshSourceOp MeshSource {
            get { return mesh_source; }
            set {
                if (mesh_source != null)
                    mesh_source.OperatorModified -= on_input_modified;
                mesh_source = value;
                if (mesh_source != null)
                    mesh_source.OperatorModified += on_input_modified;
                invalidate();
            }
        }


        ISampledCurve3dSourceOp curve_source;
        public ISampledCurve3dSourceOp CurveSource {
            get { return curve_source; }
            set {
                if (curve_source != null)
                    curve_source.OperatorModified -= on_input_modified;
                curve_source = value;
                if (curve_source != null)
                    curve_source.OperatorModified += on_input_modified;
                invalidate();
            }
        }

        protected virtual void on_input_modified(ModelingOperator op) {
            base.invalidate();
        }


        public virtual void Update()
        {
            base.begin_update();

            if (MeshSource == null)
                throw new Exception("TrimMeshFromCurveOp: must set valid MeshSource to compute!");
            if (CurveSource == null)
                throw new Exception("TrimMeshFromCurveOp: must set valid CurveSource to compute!");

            IMesh meshIn = MeshSource.GetIMesh();
            //ISpatial spatialIn = MeshSource.GetSpatial();

            DCurve3 curve = new DCurve3(CurveSource.GetICurve());

            TrimmedMesh = new DMesh3(meshIn, MeshHints.None);

            AxisAlignedBox3d bounds = TrimmedMesh.CachedBounds;
            Vector3d seed = bounds.Center + bounds.Extents.y * Vector3d.AxisY;

            MeshTrimLoop trim = new MeshTrimLoop(TrimmedMesh, curve, seed, null);
            trim.Trim();

            ApplyModifiers(TrimmedMesh);

            base.complete_update();
        }


        /*
         * IMeshSourceOp / DMeshSourceOp interface
         */

        public override IMesh GetIMesh()
        {
            if (base.requires_update())
                Update();
            return TrimmedMesh;
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
            var result = TrimmedMesh;
            TrimmedMesh = null;
            base.result_consumed();
            return result;
        }

    }
}
