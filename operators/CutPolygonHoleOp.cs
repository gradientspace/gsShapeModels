// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{
    public class CutPolygonHoleOp : BaseDMeshSourceOp
    {
        double tolerance = 0.001;
        public double Tolerance {
            get { return tolerance; }
            set {
                if (Math.Abs(tolerance - value) > MathUtil.ZeroTolerance) { tolerance = value; invalidate(); }
            }
        }
        

        double hole_size = 1.0;
        public double HoleSize {
            get { return hole_size; }
            set {
                if (Math.Abs(hole_size - value) > MathUtil.ZeroTolerance) { hole_size = value; invalidate(); }
            }
        }


        int hole_subdivisions = 16;
        public int HoleSubdivisions {
            get { return hole_subdivisions; }
            set {
                int clamped = Math.Max(3, value);
                if (clamped != hole_subdivisions) { hole_subdivisions = clamped; invalidate(); }
            }
        }


        Vector3d start_point = Vector3f.Zero;
        public Vector3d StartPoint {
            get { return start_point; }
            set {
                if (start_point.EpsilonEqual(value, MathUtil.ZeroTolerance) == false) { start_point = value; invalidate(); }
            }
        }

        Vector3d end_point = Vector3f.Zero;
        public Vector3d EndPoint {
            get { return end_point; }
            set {
                if (end_point.EpsilonEqual(value, MathUtil.ZeroTolerance) == false) { end_point = value; invalidate(); }
            }
        }



        bool through_hole = true;
        public bool ThroughHole {
            get { return through_hole; }
            set {
                if (through_hole != value) { through_hole = value; invalidate(); }
            }
        }


        double hole_depth = 10.0;
        public double HoleDepth {
            get { return hole_depth; }
            set {
                if (Math.Abs(hole_depth - value) > MathUtil.ZeroTolerance) { hole_depth = value; invalidate(); }
            }
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
                invalidate();
            }
        }


        protected virtual void on_input_modified(ModelingOperator op)
        {
            base.invalidate();
        }


        DMesh3 ResultMesh;


        public virtual void Update()
        {
            base.begin_update();
            int start_timestamp = this.CurrentInputTimestamp;

            Vector3d cache_start = start_point;
            Vector3d cache_end = end_point;
            bool cache_through = through_hole;
            double cache_depth = hole_depth;

            if (MeshSource == null)
                throw new Exception("GenerateClosedMeshOp: must set valid MeshSource to compute!");

            int try_count = 0;
            int max_tries = 0;
            try_again:

            try {
                Vector3d dir = (cache_end - cache_start).Normalized;
                if (dir.IsNormalized == false)
                    throw new Exception("Invalid direction");

                if (through_hole) {
                    ResultMesh = compute_through_hole(cache_start, cache_end, tolerance);
                } else {
                    ResultMesh = compute_partial_hole(cache_start, cache_start + cache_depth*dir, tolerance);
                }
                

                base.complete_update();

            } catch (Exception e) {

                if ( try_count < max_tries ) {
                    try_count++;
                    goto try_again;
                } 

                PostOnOperatorException(e);
                ResultMesh = base.make_failure_output(MeshSource.GetDMeshUnsafe());
                base.complete_update();
            }
        }



        DMesh3 compute_through_hole(Vector3d start, Vector3d end, double tol)
        {
            DMesh3 origMesh = MeshSource.GetDMeshUnsafe();
            DMeshAABBTree3 origSpatial = MeshSource.GetSpatial() as DMeshAABBTree3;

            DMesh3 cutMesh = new DMesh3(origMesh);

            Polygon2d polygon = Polygon2d.MakeCircle(hole_size/2, hole_subdivisions);

            Vector3f axis = (Vector3f)(start - end).Normalized;

            int start_tid = origSpatial.FindNearestTriangle(start);
            Frame3f start_frame = origMesh.GetTriFrame(start_tid);
            start_frame.Origin = (Vector3f)start;
            start_frame.AlignAxis(2, axis);

            int end_tid = origSpatial.FindNearestTriangle(end);
            //Frame3f end_frame = origMesh.GetTriFrame(end_tid); end_frame.Origin = (Vector3f)end;
            Frame3f end_frame = start_frame; end_frame.Origin = (Vector3f)end;

            MeshInsertProjectedPolygon start_insert = new MeshInsertProjectedPolygon(cutMesh, polygon, start_frame, start_tid);
            bool start_ok = start_insert.Insert();

            MeshInsertProjectedPolygon end_insert = new MeshInsertProjectedPolygon(cutMesh, polygon, end_frame, end_tid);
            bool end_ok = end_insert.Insert();

            if (start_ok == false || end_ok == false)
                throw new Exception("CutPolygonHoleOp.compute_through_hole: start or end insertion failed!");

            MeshEditor editor = new MeshEditor(cutMesh);
            EdgeLoop l0 = start_insert.InsertedLoop;
            EdgeLoop l1 = end_insert.InsertedLoop;
            l1.Reverse();
            editor.StitchLoop(l0.Vertices, l1.Vertices);

            return cutMesh;
        }







        DMesh3 compute_partial_hole(Vector3d start, Vector3d end, double tol)
        {
            DMesh3 origMesh = MeshSource.GetDMeshUnsafe();
            DMeshAABBTree3 origSpatial = MeshSource.GetSpatial() as DMeshAABBTree3;

            DMesh3 cutMesh = new DMesh3(origMesh);

            Polygon2d polygon = Polygon2d.MakeCircle(hole_size / 2, hole_subdivisions);

            Vector3f axis = (Vector3f)(start - end).Normalized;

            int start_tid = origSpatial.FindNearestTriangle(start);
            Frame3f start_frame = origMesh.GetTriFrame(start_tid);
            start_frame.Origin = (Vector3f)start;
            start_frame.AlignAxis(2, axis);

            int end_tid = origSpatial.FindNearestTriangle(end);
            //Frame3f end_frame = origMesh.GetTriFrame(end_tid); end_frame.Origin = (Vector3f)end;
            Frame3f end_frame = start_frame; end_frame.Origin = (Vector3f)end;

            // [TODO] we don't need to Simplify here...is more robust?

            MeshInsertProjectedPolygon start_insert = new MeshInsertProjectedPolygon(cutMesh, polygon, start_frame, start_tid);
            bool start_ok = start_insert.Insert();
            if (start_ok == false)
                throw new Exception("CutPolygonHoleOp.compute_partial_hole: start or end insertion failed!");

            EdgeLoop outLoop = start_insert.InsertedLoop;

            MeshExtrudeLoop extrude = new MeshExtrudeLoop(cutMesh, outLoop);
            extrude.PositionF = (v, n, vid) => {
                cutMesh.GetVertex(vid);
                return end_frame.ProjectToPlane((Vector3f)v, 2);
            };
            extrude.Extrude();

            SimpleHoleFiller filler = new SimpleHoleFiller(cutMesh, extrude.NewLoop);
            filler.Fill();

            return cutMesh;
        }








        /*
         * IMeshSourceOp / DMeshSourceOp interface
         */

        public override IMesh GetIMesh()
        {
            if (base.requires_update())
                Update();
            return ResultMesh;
        }

        public override DMesh3 GetDMeshUnsafe() {
            return (DMesh3)GetIMesh();
        }

        public override bool HasSpatial {
            get { return false; }
        }
        public override ISpatial GetSpatial()
        {
            return null;
        }

        public override DMesh3 ExtractDMesh()
        {
            Update();
            var result = ResultMesh;
            ResultMesh = null;
            base.result_consumed();
            return result;
        }



    }


}
