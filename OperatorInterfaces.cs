// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public delegate void OperatorModifiedHandler(ModelingOperator op);

    public struct ModelingOpException
    {
        public ModelingOperator op;
        public Exception e;
        public ModelingOpException(ModelingOperator op, Exception e) { this.op = op; this.e = e; }
    }

    public delegate void OperatorExceptionHandler(ModelingOpException ex);

    public interface ModelingOperator
    {
        event OperatorModifiedHandler OperatorModified;
        event OperatorExceptionHandler OperatorException;
    }

    public abstract class BaseModelingOperator : ModelingOperator
    {
        public event OperatorModifiedHandler OperatorModified;
        public event OperatorExceptionHandler OperatorException;

        virtual protected void PostOnOperatorModified()
        {
            OperatorModified?.Invoke(this);
        }

        public void ForcePostOperatorModified()
        {
            PostOnOperatorModified();
        }

        virtual protected void PostOnOperatorException(Exception e)
        {
            OperatorException?.Invoke(new ModelingOpException(this, e));
        }
    }



    /// <summary>
    /// This class handles proper invalidation of an operator's output using a timestamp.
    /// So, if an operator-modified is posted while the Update() is computing on a background
    /// thread, we won't accidentally ignore it. 
    /// 
    /// Usage is:
    ///    1) when any inputs are modified, call invalidate()
    ///    2) in any output functions, if requires_update() returns true, call your Update()
    ///    3) at start of Update(), call begin_update()
    ///    4) at end of Update(), call end_update()
    ///    5) in any function that "consumes" the output, call result_consumed()
    /// 
    /// See MeshDeformationOp for a bare-bones example 
    /// 
    /// [TODO] some kind of locking functionality, so that important data structures 
    /// cannot be modified while they are being used in Update() ?
    /// 
    /// </summary>
    public abstract class BaseSingleOutputModelingOperator : BaseModelingOperator
    {
        bool result_valid = false;
        int input_timestamp = 1;
        int last_update_timestamp = 0;


        /// <summary>
        /// Is the current valid and ready to be consumed
        /// </summary>
        public bool IsResultValid {
            get { return result_valid; }
        }

        /// <summary>
        /// The Timestamp of the last client request. This value is incremented on invalidate() calls.
        /// </summary>
        public int CurrentInputTimestamp {
            get { return input_timestamp; }
        }

        /// <summary>
        /// The value of CurrentInputTimestamp the last time we began an update().
        /// If this is == CurrentInputTimestamp, then the result is up-to-date
        /// If this is &lt CurrentInputTimestamp, we need to Update()
        /// </summary>
        public int LastUpdateTimestamp {
            get { return last_update_timestamp; }
        }


        /// <summary>
        /// force an invalidation. Client may need to notify op about external change events, 
        /// for example requests to explicitly cancel current computation
        /// </summary>
        public virtual void ForceInvalidate()
        {
            invalidate();
        }


        protected virtual void invalidate()
        {
            result_valid = false;
            input_timestamp++;
            PostOnOperatorModified();
        }

        protected virtual void begin_update()
        {
            last_update_timestamp = input_timestamp;
        }

        protected virtual void complete_update()
        {
            result_valid = true;
        }

        protected virtual bool requires_update()
        {
            return (result_valid == false || input_timestamp != last_update_timestamp);
        }

        protected virtual bool is_invalidated()
        {
            return input_timestamp != last_update_timestamp;
        }

        protected virtual void result_consumed()
        {
            result_valid = false;
        }
    }




    public interface IMeshSourceOp : ModelingOperator
    {
        IMesh GetIMesh();

        bool HasSpatial { get; }
        ISpatial GetSpatial();
    }

    public interface DMeshSourceOp : IMeshSourceOp
    {
        /// <summary>
        /// returns DMesh that you should *not* be modifying
        /// </summary>
        DMesh3 GetDMeshUnsafe();


        /// <summary>
        /// assumption is that we can edit this DMesh3, ie the op no longer holds a reference!
        /// </summary>
        DMesh3 ExtractDMesh();
    }



    public interface ISampledCurve3dSourceOp : ModelingOperator
    {
        ISampledCurve3d GetICurve();
    }

    public interface DCurve3SourceOp : ISampledCurve3dSourceOp
    {
        /// <summary>
        /// assumption is that we can edit this DCurve3, ie the op no longer holds a reference!
        /// </summary>
        DCurve3 ExtractDCurve();
    }




    public interface IIndexListSourceOp : ModelingOperator
    {
        IList<int> GetIndices();
    }


    public interface IVectorDisplacementSourceOp : ModelingOperator
    {
        IVectorDisplacement GetDisplacement();
    }




    public struct DMeshOutputStatus
    {
        public enum States {
            Unavailable, Computing, Ready
        }
        public States State;
        public DMesh3 Mesh;
        public List<ModelingOpException> ComputeExceptions;

        public bool IsErrorOutput() {
            return Mesh == null || DMeshOpUtil.IsFailureMesh(Mesh);
        }

        public static DMeshOutputStatus Unavailable() { return new DMeshOutputStatus() { State = States.Unavailable }; }
        public static DMeshOutputStatus Computing() { return new DMeshOutputStatus() { State = States.Computing }; }
        public static DMeshOutputStatus Ready(DMesh3 mesh) { return new DMeshOutputStatus() { State = States.Ready, Mesh = mesh }; }
        public static DMeshOutputStatus Ready(DMesh3 mesh, List<ModelingOpException> exceptions)
            { return new DMeshOutputStatus() { State = States.Ready, Mesh = mesh, ComputeExceptions = exceptions }; }
    }


    public interface DMeshComputeOp : ModelingOperator
    {
        // this function will return null if no new mesh is available yet
        // (is this good? weird! but means we can do in separate thread safely...)
        DMeshOutputStatus CheckForNewMesh();
    }



    public interface ResultSourceOp<T> : ModelingOperator
    {
        T ExtractResult();
    }




    public enum OpResultState
    {
        Unavailable, Computing, Ready
    }

    public struct OpResultOutputStatus<T>
    {
        public OpResultState State;
        public T Result;
        public List<ModelingOpException> ComputeExceptions;

        public static OpResultOutputStatus<T> Unavailable() { return new OpResultOutputStatus<T>() { State = OpResultState.Unavailable }; }
        public static OpResultOutputStatus<T> Computing() { return new OpResultOutputStatus<T>() { State = OpResultState.Computing }; }
        public static OpResultOutputStatus<T> Ready(T result) { return new OpResultOutputStatus<T>() { State = OpResultState.Ready, Result = result}; }
        public static OpResultOutputStatus<T> Ready(T result, List<ModelingOpException> exceptions)
            { return new OpResultOutputStatus<T>() { State = OpResultState.Ready, Result = result, ComputeExceptions = exceptions }; }

    }

    public interface ResultComputeOp<T> : ModelingOperator
    {
        OpResultOutputStatus<T> CheckForNewResult();
    }


}
