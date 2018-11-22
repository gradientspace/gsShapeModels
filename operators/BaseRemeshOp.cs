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
    /// <summary>
    /// RemeshOp implements basic remeshing pass.
    /// 
    /// [TODO] for remeshed boundary mode, we are recomputing MeshBoundaryLoops each time...
    /// 
    /// </summary>
    public abstract class BaseRemeshOp : BaseDMeshSourceOp
    {
         bool prevent_normal_flips = true;
        public virtual bool PreventNormalFlips {
            get { return prevent_normal_flips; }
            set { if (prevent_normal_flips != value) { prevent_normal_flips = value; invalidate(); } }
        }


        bool enable_flips = true;
        public virtual bool EnableFlips {
            get { return enable_flips; }
            set { if (enable_flips != value) { enable_flips = value; invalidate(); } }
        }

        bool enable_splits = true;
        public virtual bool EnableSplits {
            get { return enable_splits; }
            set { if (enable_splits != value) { enable_splits = value; invalidate(); } }
        }

        bool enable_collapses = true;
        public virtual bool EnableCollapses {
            get { return enable_collapses; }
            set { if (enable_collapses != value) { enable_collapses = value; invalidate(); } }
        }

        bool enable_smoothing = true;
        public virtual bool EnableSmoothing {
            get { return enable_smoothing; }
            set { if (enable_smoothing != value) { enable_smoothing = value; invalidate(); } }
        }

        double smooth_speed = 0.5;
        public virtual double SmoothingSpeed {
            get { return smooth_speed; }
            set {
                double set_speed = MathUtil.Clamp(value, 0.0, 1.0);
                if (Math.Abs(smooth_speed - set_speed) > MathUtil.ZeroTolerancef) { smooth_speed = set_speed; invalidate(); }
            }
        }

        public enum BoundaryModes
        {
            FreeBoundaries = 0,
            FixedBoundaries = 1,
            ConstrainedBoundaries = 2
        }
        BoundaryModes boundary_mode = BoundaryModes.FreeBoundaries;
        public virtual BoundaryModes BoundaryMode {
            get { return boundary_mode; }
            set { if (boundary_mode != value) { boundary_mode = value; invalidate(); } }
        }


        protected virtual void on_input_modified(ModelingOperator op)
        {
            base.invalidate();
        }


        protected abstract DMesh3 GetResultMesh(bool bConsume);
        public abstract void Update();



        /*
         * IMeshSourceOp / DMeshSourceOp interface
         */

        public override IMesh GetIMesh()
        {
            if (base.requires_update())
                Update();
            return GetResultMesh(false);
        }

        public override DMesh3 GetDMeshUnsafe()
        {
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
            var result = GetResultMesh(true);
            base.result_consumed();
            return result;
        }




    }


}