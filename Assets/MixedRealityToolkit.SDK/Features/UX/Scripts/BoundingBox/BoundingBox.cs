﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Input;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityPhysics = UnityEngine.Physics;
using Microsoft.MixedReality.Toolkit.UI.BoundingBoxTypes;

namespace Microsoft.MixedReality.Toolkit.UI
{
    [HelpURL("https://microsoft.github.io/MixedRealityToolkit-Unity/Documentation/README_BoundingBox.html")]
    public class BoundingBox : MonoBehaviour,
        IMixedRealitySourceStateHandler,
        IMixedRealityFocusChangedHandler,
        IMixedRealityFocusHandler
    {
        #region Serialized Fields and Properties
        [SerializeField]
        [Tooltip("The object that the bounding box rig will be modifying.")]
        private GameObject targetObject;

        [Tooltip("For complex objects, automatic bounds calculation may not behave as expected. Use an existing Box Collider (even on a child object) to manually determine bounds of Bounding Box.")]
        [SerializeField]
        [FormerlySerializedAs("BoxColliderToUse")]
        private BoxCollider boundsOverride = null;

        /// <summary>
        /// For complex objects, automatic bounds calculation may not behave as expected. Use an existing Box Collider (even on a child object) to manually determine bounds of Bounding Box.
        /// </summary>
        public BoxCollider BoundsOverride
        {
            get { return boundsOverride; }
            set
            {
                if (boundsOverride != value)
                {
                    boundsOverride = value;

                    if (boundsOverride == null)
                    {
                        prevBoundsOverride = new Bounds();
                    }
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [Tooltip("Defines the volume type and the priority for the bounds calculation")]
        private BoundsCalculationMethod boundsCalculationMethod = BoundsCalculationMethod.RendererOverCollider;

        /// <summary>
        /// Defines the volume type and the priority for the bounds calculation
        /// </summary>
        public BoundsCalculationMethod CalculationMethod
        {
            get { return boundsCalculationMethod; }
            set
            {
                if (boundsCalculationMethod != value)
                {
                    boundsCalculationMethod = value;
                    CreateRig();
                }
            }
        }

        [Header("Behavior")]
        [SerializeField]
        [Tooltip("Type of activation method for showing/hiding bounding box handles and controls")]
        private BoundingBoxActivationType activation = BoundingBoxActivationType.ActivateOnStart;

        /// <summary>
        /// Type of activation method for showing/hiding bounding box handles and controls
        /// </summary>
        public BoundingBoxActivationType BoundingBoxActivation
        {
            get { return activation; }
            set
            {
                if (activation != value)
                {
                    activation = value;
                    ResetHandleVisibility();
                }
            }
        }

        [SerializeField]
        [Obsolete("Use a TransformScaleHandler script rather than setting minimum on BoundingBox directly", false)]
        [Tooltip("Minimum scaling allowed relative to the initial size")]
        private float scaleMinimum = 0.2f;

        [SerializeField]
        [Obsolete("Use a TransformScaleHandler script rather than setting maximum on BoundingBox directly")]
        [Tooltip("Maximum scaling allowed relative to the initial size")]
        private float scaleMaximum = 2.0f;


        /// <summary>
        /// Public property for the scale minimum, in the target's local scale.
        /// Set this value with SetScaleLimits.
        /// </summary>
        [Obsolete("Use a TransformScaleHandler.ScaleMinimum as it is the authoritative value for min scale")]
        public float ScaleMinimum
        {
            get
            {
                if (scaleHandler != null)
                {
                    return scaleHandler.ScaleMinimum;
                }
                return 0.0f;
            }
        }

        /// <summary>
        /// Public property for the scale maximum, in the target's local scale.
        /// Set this value with SetScaleLimits.
        /// </summary>
        [Obsolete("Use a TransformScaleHandler.ScaleMinimum as it is the authoritative value for max scale")]
        public float ScaleMaximum
        {
            get
            {
                if (scaleHandler != null)
                {
                    return scaleHandler.ScaleMaximum;
                }
                return 0.0f;
            }
        }

        [Header("Box Display")]
        [SerializeField]
        [Tooltip("Flatten bounds in the specified axis or flatten the smallest one if 'auto' is selected")]
        private FlattenModeType flattenAxis = FlattenModeType.DoNotFlatten;

        /// <summary>
        /// Flatten bounds in the specified axis or flatten the smallest one if 'auto' is selected
        /// </summary>
        public FlattenModeType FlattenAxis
        {
            get { return flattenAxis; }
            set
            {
                if (flattenAxis != value)
                {
                    flattenAxis = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [Tooltip("When an axis is flattened what value to set that axis's scale to for display.")]
        private float flattenAxisDisplayScale = 0.0f;

        /// <summary>
        /// When an axis is flattened what value to set that axis's scale to for display.
        /// </summary>
        public float FlattenAxisDisplayScale
        {
            get { return flattenAxisDisplayScale; }
            set
            {
                if (flattenAxisDisplayScale != value)
                {
                    flattenAxisDisplayScale = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("wireframePadding")]
        [Tooltip("Extra padding added to the actual Target bounds")]
        private Vector3 boxPadding = Vector3.zero;

        /// <summary>
        /// Extra padding added to the actual Target bounds
        /// </summary>
        public Vector3 BoxPadding
        {
            get { return boxPadding; }
            set
            {
                if (Vector3.Distance(boxPadding, value) > float.Epsilon)
                {
                    boxPadding = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [Tooltip("Material used to display the bounding box. If set to null no bounding box will be displayed")]
        private Material boxMaterial = null;

        /// <summary>
        /// Material used to display the bounding box. If set to null no bounding box will be displayed
        /// </summary>
        public Material BoxMaterial
        {
            get { return boxMaterial; }
            set
            {
                if (boxMaterial != value)
                {
                    boxMaterial = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [Tooltip("Material used to display the bounding box when grabbed. If set to null no change will occur when grabbed.")]
        private Material boxGrabbedMaterial = null;

        /// <summary>
        /// Material used to display the bounding box when grabbed. If set to null no change will occur when grabbed.
        /// </summary>
        public Material BoxGrabbedMaterial
        {
            get { return boxGrabbedMaterial; }
            set
            {
                if (boxGrabbedMaterial != value)
                {
                    boxGrabbedMaterial = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [Tooltip("Show a wireframe around the bounding box when checked. Wireframe parameters below have no effect unless this is checked")]
        private bool showWireframe = true;

        /// <summary>
        /// Show a wireframe around the bounding box when checked. Wireframe parameters below have no effect unless this is checked
        /// </summary>
        public bool ShowWireFrame
        {
            get { return showWireframe; }
            set
            {
                if (showWireframe != value)
                {
                    showWireframe = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [Tooltip("Shape used for wireframe display")]
        private WireframeType wireframeShape = WireframeType.Cubic;

        /// <summary>
        /// Shape used for wireframe display
        /// </summary>
        public WireframeType WireframeShape
        {
            get { return wireframeShape; }
            set
            {
                if (wireframeShape != value)
                {
                    wireframeShape = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [Tooltip("Material used for wireframe display")]
        private Material wireframeMaterial;

        /// <summary>
        /// Material used for wireframe display
        /// </summary>
        public Material WireframeMaterial
        {
            get { return wireframeMaterial; }
            set
            {
                if (wireframeMaterial != value)
                {
                    wireframeMaterial = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [FormerlySerializedAs("linkRadius")]
        [Tooltip("Radius for wireframe edges")]
        private float wireframeEdgeRadius = 0.001f;

        /// <summary>
        /// Radius for wireframe edges
        /// </summary>
        public float WireframeEdgeRadius
        {
            get { return wireframeEdgeRadius; }
            set
            {
                if (wireframeEdgeRadius != value)
                {
                    wireframeEdgeRadius = value;
                    CreateRig();
                }
            }
        }

        [Header("Handles")]
        [SerializeField]
        [Tooltip("Material applied to hansdles when they are not in a grabbed state")]
        private Material handleMaterial;

        /// <summary>
        /// Material applied to handles when they are not in a grabbed state
        /// </summary>
        public Material HandleMaterial
        {
            get { return handleMaterial; }
            set
            {
                if (handleMaterial != value)
                {
                    handleMaterial = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [Tooltip("Material applied to handles while they are a grabbed")]
        private Material handleGrabbedMaterial;

        /// <summary>
        /// Material applied to handles while they are a grabbed
        /// </summary>
        public Material HandleGrabbedMaterial
        {
            get { return handleGrabbedMaterial; }
            set
            {
                if (handleGrabbedMaterial != value)
                {
                    handleGrabbedMaterial = value;
                    CreateRig();
                }
            }
        }



        [SerializeField]
        [Tooltip("TODO TOOLTIP")]
        BoundingBoxHandles scaleHandles = new BoundingBoxHandles();

        

    

       


        [SerializeField]
        [Tooltip("Prefab used to display rotation handles in the midpoint of each edge. Aligns the Y axis of the prefab with the pivot axis, and the X and Z axes pointing outward. If not set, spheres will be displayed instead")]
        private GameObject rotationHandlePrefab = null;

        /// <summary>
        /// Prefab used to display rotation handles in the midpoint of each edge. Aligns the Y axis of the prefab with the pivot axis, and the X and Z axes pointing outward. If not set, spheres will be displayed instead
        /// </summary>
        public GameObject RotationHandleSlatePrefab
        {
            get { return rotationHandlePrefab; }
            set
            {
                if (rotationHandlePrefab != value)
                {
                    rotationHandlePrefab = value;
                    CreateRig();
                }
            }
        }
        [SerializeField]
        [FormerlySerializedAs("ballRadius")]
        [Tooltip("Radius of the handle geometry of rotation handles")]
        private float rotationHandleSize = 0.016f; // 1.6cm default handle size

        /// <summary>
        /// Radius of the handle geometry of rotation handles
        /// </summary>
        public float RotationHandleSize
        {
            get { return rotationHandleSize; }
            set
            {
                if (rotationHandleSize != value)
                {
                    rotationHandleSize = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [Tooltip("Additional padding to apply to the collider on rotate handle to make handle easier to hit")]
        private Vector3 rotateHandleColliderPadding = new Vector3(0.016f, 0.016f, 0.016f);

        /// <summary>
        /// Additional padding to apply to the collider on rotate handle to make handle easier to hit
        /// </summary>
        public Vector3 RotateHandleColliderPadding
        {
            get { return rotateHandleColliderPadding; }
            set
            {
                if (rotateHandleColliderPadding != value)
                {
                    rotateHandleColliderPadding = value;
                    CreateRig();
                }
            }
        }

        BoundingBoxVisuals visuals;

        [SerializeField]
        [Tooltip("Determines the type of collider that will surround the rotation handle prefab.")]
        private RotationHandlePrefabCollider rotationHandlePrefabColliderType = RotationHandlePrefabCollider.Box;

        /// <summary>
        /// Determines the type of collider that will surround the rotation handle prefab.
        /// </summary>
        public RotationHandlePrefabCollider RotationHandlePrefabColliderType
        {
            get
            {
                return rotationHandlePrefabColliderType;
            }
            set
            {
                if (rotationHandlePrefabColliderType != value)
                {
                    rotationHandlePrefabColliderType = value;
                    CreateRig();
                }
            }
        }

        [SerializeField]
        [Tooltip("Check to show scale handles")]
        private bool showScaleHandles = true;

        /// <summary>
        /// Public property to Set the visibility of the corner cube Scaling handles.
        /// This property can be set independent of the Rotate handles.
        /// </summary>
        public bool ShowScaleHandles
        {
            get
            {
                return showScaleHandles;
            }
            set
            {
                if (showScaleHandles != value)
                {
                    showScaleHandles = value;
                    ResetHandleVisibility();
                }
            }
        }

        [SerializeField]
        [Tooltip("Check to show rotation handles for the X axis")]
        private bool showRotationHandleForX = true;

        /// <summary>
        /// Check to show rotation handles for the X axis
        /// </summary>
        public bool ShowRotationHandleForX
        {
            get
            {
                return showRotationHandleForX;
            }
            set
            {
                if (showRotationHandleForX != value)
                {
                    showRotationHandleForX = value;
                    ResetHandleVisibility();
                }
            }
        }

        [SerializeField]
        [Tooltip("Check to show rotation handles for the Y axis")]
        private bool showRotationHandleForY = true;

        /// <summary>
        /// Check to show rotation handles for the Y axis
        /// </summary>
        public bool ShowRotationHandleForY
        {
            get
            {
                return showRotationHandleForY;
            }
            set
            {
                if (showRotationHandleForY != value)
                {
                    showRotationHandleForY = value;
                    ResetHandleVisibility();
                }
            }
        }

        [SerializeField]
        [Tooltip("Check to show rotation handles for the Z axis")]
        private bool showRotationHandleForZ = true;

        /// <summary>
        /// Check to show rotation handles for the Z axis
        /// </summary>
        public bool ShowRotationHandleForZ
        {
            get
            {
                return showRotationHandleForZ;
            }
            set
            {
                if (showRotationHandleForZ != value)
                {
                    showRotationHandleForZ = value;
                    ResetHandleVisibility();
                }
            }
        }

        [SerializeField]
        [Tooltip("Check to draw a tether point from the handles to the hand when manipulating.")]
        private bool drawTetherWhenManipulating = true;

        /// <summary>
        /// Check to draw a tether point from the handles to the hand when manipulating.
        /// </summary>
        public bool DrawTetherWhenManipulating
        {
            get
            {
                return drawTetherWhenManipulating;
            }
            set
            {
                drawTetherWhenManipulating = value;
            }
        }



        [SerializeField]
        [Tooltip("Add a Collider here if you do not want the handle colliders to interact with another object's collider.")]
        private Collider handlesIgnoreCollider = null;

        /// <summary>
        /// Add a Collider here if you do not want the handle colliders to interact with another object's collider.
        /// </summary>
        public Collider HandlesIgnoreCollider
        {
            get
            {
                return handlesIgnoreCollider;
            }
            set
            {
                handlesIgnoreCollider = value;
            }
        }

        [Header("Debug")]
        [Tooltip("Debug only. Component used to display debug messages")]
        public TextMesh debugText;

        [SerializeField]
        [Tooltip("Determines whether to hide GameObjects (i.e handles, links etc) created and managed by this component in the editor")]
        private bool hideElementsInInspector = true;

        /// <summary>
        /// Determines whether to hide GameObjects (i.e handles, links etc) created and managed by this component in the editor
        /// </summary>
        public bool HideElementsInInspector
        {
            get { return hideElementsInInspector; }
            set
            {
                if (hideElementsInInspector != value)
                {
                    hideElementsInInspector = value;
                    UpdateRigVisibilityInInspector();
                }
            }
        }

        [SerializeField]
        [Tooltip("Configuration for Proximity Effect")]
        public BoundingBoxProximityEffect proximityEffect = new BoundingBoxProximityEffect();

        [Header("Events")]
        public UnityEvent RotateStarted = new UnityEvent();
        public UnityEvent RotateStopped = new UnityEvent();
        public UnityEvent ScaleStarted = new UnityEvent();
        public UnityEvent ScaleStopped = new UnityEvent();


        #endregion Serialized Fields


        public BoxCollider TargetBounds
        {
            get
            {
                return boundingBoxTarget.TargetBounds;
            }
        }

        public GameObject Target
        {
            get
            {
                return boundingBoxTarget.Target;
            }
        }

        #region Private Fields


      

        // Whether we should be displaying just the wireframe (if enabled) or the handles too
        private bool wireframeOnly = false;

        // Pointer that is being used to manipulate the bounding box
        private IMixedRealityPointer currentPointer;

        private Transform rigRoot;

        // Game object used to display the bounding box. Parented to the rig root
        private GameObject boxDisplay;

        

        // Half the size of the current bounds
        private Vector3 currentBoundsExtents;

        private readonly List<IMixedRealityInputSource> touchingSources = new List<IMixedRealityInputSource>();

        
        private List<IMixedRealityController> sourcesDetected;
        

        // Current axis of rotation about the center of the rig root
        private Vector3 currentRotationAxis;

        // Scale of the target at the beginning of the current manipulation
        private Vector3 initialScaleOnGrabStart;

        // Position of the target at the beginning of the current manipulation
        private Vector3 initialPositionOnGrabStart;

        // Point that was initially grabbed in OnPointerDown()
        private Vector3 initialGrabPoint;

        // Current position of the grab point
        private Vector3 currentGrabPoint;

        private TransformScaleHandler scaleHandler;

        // Grab point position in pointer space. Used to calculate the current grab point from the current pointer pose.
        private Vector3 grabPointInPointer;

        
        private int[] flattenedHandles;

        // Corner opposite to the grabbed one. Scaling will be relative to it.
        private Vector3 oppositeCorner;

        // Direction of the diagonal from the opposite corner to the grabbed one.
        private Vector3 diagonalDir;

        private HandleType currentHandleType;

        // The size, position of boundsOverride object in the previous frame
        // Used to determine if boundsOverride size has changed.
        private Bounds prevBoundsOverride = new Bounds();

        // True if this game object is a child of the Target one
        private bool isChildOfTarget = false;
        private static readonly string rigRootName = "rigRoot";

        // Cache for the corner points of either renderers or colliders during the bounds calculation phase
        private static List<Vector3> totalBoundsCorners = new List<Vector3>();

        


        
        #endregion

        #region public Properties
        // TODO Review this, it feels like we should be using Behaviour.enabled instead.
        private bool active = false;
        public bool Active
        {
            get
            {
                return active;
            }
            set
            {
                if (active != value)
                {
                    active = value;
                    rigRoot?.gameObject.SetActive(value);
                    ResetHandleVisibility();

                    if (value)
                        proximityEffect.ResetHandleProximityScale(this);

                   
                }
            }
        }

       

        

        #endregion Public Properties


        #region Public Methods

        private BoundingBoxTarget boundingBoxTarget;
        public void Awake()
        {
            if (targetObject == null)
                targetObject = gameObject;
            boundingBoxTarget = new BoundingBoxTarget(targetObject ? targetObject : gameObject);
        }

        

        /// <summary>
        /// Sets the minimum/maximum scale for the bounding box at runtime.
        /// </summary>
        /// <param name="min">Minimum scale</param>
        /// <param name="max">Maximum scale</param>
        /// <param name="relativeToInitialState">If true the values will be multiplied by scale of target at startup. If false they will be in absolute local scale.</param>
        [Obsolete("Use a TransformScaleHandler script rather than setting min/max scale on BoundingBox directly")]
        public void SetScaleLimits(float min, float max, bool relativeToInitialState = true)
        {
            scaleMinimum = min;
            scaleMaximum = max;
        }

        

        #endregion


        #region MonoBehaviour Methods

        private void OnEnable()
        {
            CreateRig();
            CaptureInitialState();

            if (activation == BoundingBoxActivationType.ActivateByProximityAndPointer ||
                activation == BoundingBoxActivationType.ActivateByProximity ||
                activation == BoundingBoxActivationType.ActivateByPointer)
            {
                wireframeOnly = true;
                Active = true;
            }
            else if (activation == BoundingBoxActivationType.ActivateOnStart)
            {
                Active = true;
            }
            else if (activation == BoundingBoxActivationType.ActivateManually)
            {
                //activate to create handles etc. then deactivate. 
                Active = true;
                Active = false;
            }
        }

        private void OnDisable()
        {
            DestroyRig();
        }

        private void Update()
        {
            if (active)
            {
                if (currentPointer != null)
                {
                    TransformTarget(currentHandleType);
                    UpdateBounds();
                    UpdateRigHandles();
                }
                else if (!isChildOfTarget && boundingBoxTarget.HasChanged())
                {
                    UpdateBounds();
                    UpdateRigHandles();
                    boundingBoxTarget.ClearChanged();
                }


                // Only update proximity scaling of handles if they are visible which is when
                // active is true and wireframeOnly is false
                if (!wireframeOnly)
                {
                    // If any handle type is visible, then update
                    if (ShowScaleHandles || ShowRotationHandleForX || ShowRotationHandleForY || ShowRotationHandleForZ)
                    {
                        proximityEffect.HandleProximityScaling(this, currentPointer, currentBoundsExtents);
                    }
                }
            }
            else if (boundsOverride != null && HasBoundsOverrideChanged())
            {
                UpdateBounds();
                UpdateRigHandles();
            }
        }

        /// <summary>
        /// Assumes that boundsOverride is not null
        /// Returns true if the size / location of boundsOverride has changed.
        /// If boundsOverride gets set to null, rig is re-created in BoundsOverride
        /// property setter.
        /// </summary>
        private bool HasBoundsOverrideChanged()
        {
            Debug.Assert(boundsOverride != null, "HasBoundsOverrideChanged called but boundsOverride is null");
            Bounds curBounds = boundsOverride.bounds;
            bool result = curBounds != prevBoundsOverride;
            prevBoundsOverride = curBounds;
            return result;
        }

        #endregion MonoBehaviour Methods


        #region Private Methods

        

       

        
       


        

        /// <summary>
        /// Helper method to check if handle type may be visible based on configuration
        /// </summary>
        /// <param name="h">handle reference to check</param>
        /// <returns>true if potentially visible, false otherwise</returns>
        public bool IsHandleTypeVisible(HandleType type)
        {
            return (type == HandleType.Scale && ShowScaleHandles) ||
                (type == HandleType.Rotation && (ShowRotationHandleForX || ShowRotationHandleForY || ShowRotationHandleForZ));
        }

        private void SetBoundingBoxCollider()
        {
            // Make sure that the bounds of all child objects are up to date before we compute bounds
            UnityPhysics.SyncTransforms();

            if (boundsOverride != null)
            {
                boundingBoxTarget.TargetBounds = boundsOverride;
                boundingBoxTarget.TargetBounds.transform.hasChanged = true;
            }
            else
            {
                Bounds bounds = GetTargetBounds();
                boundingBoxTarget.TargetBounds = boundingBoxTarget.Target.AddComponent<BoxCollider>();

                boundingBoxTarget.TargetBounds.center = bounds.center;
                boundingBoxTarget.TargetBounds.size = bounds.size;
            }

            CalculateBoxPadding();

            boundingBoxTarget.TargetBounds.EnsureComponent<NearInteractionGrabbable>();
        }

        private void CalculateBoxPadding()
        {
            if (boxPadding == Vector3.zero) { return; }

            Vector3 scale = boundingBoxTarget.TargetBounds.transform.lossyScale;

            for (int i = 0; i < 3; i++)
            {
                if (scale[i] == 0f) { return; }

                scale[i] = 1f / scale[i];
            }

            boundingBoxTarget.TargetBounds.size += Vector3.Scale(boxPadding, scale);
        }

        private Bounds GetTargetBounds()
        {
            KeyValuePair<Transform, Collider> colliderByTransform;
            KeyValuePair<Transform, Bounds> rendererBoundsByTransform;
            totalBoundsCorners.Clear();

            // Collect all Transforms except for the rigRoot(s) transform structure(s)
            // Its possible we have two rigRoots here, the one about to be deleted and the new one
            // Since those have the gizmo structure childed, be need to ommit them completely in the calculation of the bounds
            // This can only happen by name unless there is a better idea of tracking the rigRoot that needs destruction

            List<Transform> childTransforms = new List<Transform>();
            childTransforms.Add(boundingBoxTarget.Target.transform);

            foreach (Transform childTransform in boundingBoxTarget.Target.transform)
            {
                if (childTransform.name.Equals(rigRootName)) { continue; }
                childTransforms.AddRange(childTransform.GetComponentsInChildren<Transform>());
            }

            // Iterate transforms and collect bound volumes

            foreach (Transform childTransform in childTransforms)
            {
                Debug.Assert(childTransform != rigRoot);

                if (boundsCalculationMethod != BoundsCalculationMethod.RendererOnly)
                {
                    Collider collider = childTransform.GetComponent<Collider>();
                    if (collider != null)
                    {
                        colliderByTransform = new KeyValuePair<Transform, Collider>(childTransform, collider);
                    }
                    else
                    {
                        continue;
                    }
                }

                if (boundsCalculationMethod != BoundsCalculationMethod.ColliderOnly)
                {
                    MeshFilter meshFilter = childTransform.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        rendererBoundsByTransform = new KeyValuePair<Transform, Bounds>(childTransform, meshFilter.sharedMesh.bounds);
                    }
                    else
                    {
                        continue;
                    }
                }

                // Encapsulate the collider bounds if criteria match

                if (boundsCalculationMethod == BoundsCalculationMethod.ColliderOnly ||
                    boundsCalculationMethod == BoundsCalculationMethod.ColliderOverRenderer)
                {
                    AddColliderBoundsToTarget(colliderByTransform);
                    if (boundsCalculationMethod == BoundsCalculationMethod.ColliderOnly) { continue; }
                }

                // Encapsulate the renderer bounds if criteria match

                if (boundsCalculationMethod != BoundsCalculationMethod.ColliderOnly)
                {
                    AddRendererBoundsToTarget(rendererBoundsByTransform);
                    if (boundsCalculationMethod == BoundsCalculationMethod.RendererOnly) { continue; }
                }

                // Do the collider for the one case that we chose RendererOverCollider and did not find a renderer
                AddColliderBoundsToTarget(colliderByTransform);
            }

            if (totalBoundsCorners.Count == 0) { return new Bounds(); }

            Transform targetTransform = boundingBoxTarget.Target.transform;

            Bounds finalBounds = new Bounds(targetTransform.InverseTransformPoint(totalBoundsCorners[0]), Vector3.zero);

            for (int i = 1; i < totalBoundsCorners.Count; i++)
            {
                finalBounds.Encapsulate(targetTransform.InverseTransformPoint(totalBoundsCorners[i]));
            }

            return finalBounds;
        }

        private void AddRendererBoundsToTarget(KeyValuePair<Transform, Bounds> rendererBoundsByTarget)
        {
            Vector3[] cornersToWorld = null;
            rendererBoundsByTarget.Value.GetCornerPositions(rendererBoundsByTarget.Key, ref cornersToWorld);
            totalBoundsCorners.AddRange(cornersToWorld);
        }

        private void AddColliderBoundsToTarget(KeyValuePair<Transform, Collider> colliderByTransform)
        {
            BoundsExtensions.GetColliderBoundsPoints(colliderByTransform.Value, totalBoundsCorners, 0);
        }

        

      

        

        private void CaptureInitialState()
        {
         //   var target = Target;
            if (boundingBoxTarget != null)
            {
                isChildOfTarget = transform.IsChildOf(boundingBoxTarget.Target.transform);

                scaleHandler = GetComponent<TransformScaleHandler>();
                if (scaleHandler == null)
                {
                    scaleHandler = gameObject.AddComponent<TransformScaleHandler>();

                    scaleHandler.TargetTransform = boundingBoxTarget.transform;
                #pragma warning disable 0618
                    scaleHandler.ScaleMinimum = scaleMinimum;
                    scaleHandler.ScaleMaximum = scaleMaximum;
                #pragma warning restore 0618
                }
            }
        }

       

       

        

        private void UpdateBounds()
        {
            if (boundingBoxTarget.TargetBounds != null)
            {
                // Store current rotation then zero out the rotation so that the bounds
                // are computed when the object is in its 'axis aligned orientation'.
                Quaternion currentRotation = boundingBoxTarget.Target.transform.rotation;
                boundingBoxTarget.Target.transform.rotation = Quaternion.identity;
                UnityPhysics.SyncTransforms(); // Update collider bounds

                Vector3 boundsExtents = boundingBoxTarget.TargetBounds.bounds.extents;

                // After bounds are computed, restore rotation...
                boundingBoxTarget.transform.rotation = currentRotation;
                UnityPhysics.SyncTransforms();

                if (boundsExtents != Vector3.zero)
                {
                    if (flattenAxis == FlattenModeType.FlattenAuto)
                    {
                        float min = Mathf.Min(boundsExtents.x, Mathf.Min(boundsExtents.y, boundsExtents.z));
                        flattenAxis = (min == boundsExtents.x) ? FlattenModeType.FlattenX :
                            ((min == boundsExtents.y) ? FlattenModeType.FlattenY : FlattenModeType.FlattenZ);
                    }

                    boundsExtents.x = (flattenAxis == FlattenModeType.FlattenX) ? 0.0f : boundsExtents.x;
                    boundsExtents.y = (flattenAxis == FlattenModeType.FlattenY) ? 0.0f : boundsExtents.y;
                    boundsExtents.z = (flattenAxis == FlattenModeType.FlattenZ) ? 0.0f : boundsExtents.z;
                    currentBoundsExtents = boundsExtents;

                    GetCornerPositionsFromBounds(new Bounds(Vector3.zero, boundsExtents * 2.0f), ref boundsCorners);
                    CalculateEdgeCenters(boundsCorners);
                }
            }
        }

        
       


       

 



        

        private void GetCornerPositionsFromBounds(Bounds bounds, ref Vector3[] positions)
        {
            int numCorners = 1 << 3;
            if (positions == null || positions.Length != numCorners)
            {
                positions = new Vector3[numCorners];
            }

            // Permutate all axes using minCorner and maxCorner.
            Vector3 minCorner = bounds.center - bounds.extents;
            Vector3 maxCorner = bounds.center + bounds.extents;
            for (int c = 0; c < numCorners; c++)
            {
                positions[c] = new Vector3(
                    (c & (1 << 0)) == 0 ? minCorner[0] : maxCorner[0],
                    (c & (1 << 1)) == 0 ? minCorner[1] : maxCorner[1],
                    (c & (1 << 2)) == 0 ? minCorner[2] : maxCorner[2]);
            }
        }

        

        private bool DoesActivationMatchFocus(FocusEventData eventData)
        {
            switch (activation)
            {
                case BoundingBoxActivationType.ActivateOnStart:
                case BoundingBoxActivationType.ActivateManually:
                    return false;
                case BoundingBoxActivationType.ActivateByProximity:
                    return eventData.Pointer is IMixedRealityNearPointer;
                case BoundingBoxActivationType.ActivateByPointer:
                    return eventData.Pointer is IMixedRealityPointer;
                case BoundingBoxActivationType.ActivateByProximityAndPointer:
                    return true;
                default:
                    return false;
            }
        }

        private void DropController()
        {
            HandleType lastHandleType = currentHandleType;
            currentPointer = null;
            currentHandleType = HandleType.None;
            ResetHandleVisibility();

            if (lastHandleType == HandleType.Scale)
            {
                if (debugText != null) debugText.text = "OnPointerUp:ScaleStopped";
                ScaleStopped?.Invoke();
            }
            else if (lastHandleType == HandleType.Rotation)
            {
                if (debugText != null) debugText.text = "OnPointerUp:RotateStopped";
                RotateStopped?.Invoke();
            }
        }

        #endregion Private Methods


        #region Used Event Handlers

        void IMixedRealityFocusChangedHandler.OnFocusChanged(FocusEventData eventData)
        {
            if (eventData.NewFocusedObject == null)
            {
                proximityEffect.ResetHandleProximityScale(this);
            }

            if (activation == BoundingBoxActivationType.ActivateManually || activation == BoundingBoxActivationType.ActivateOnStart)
            {
                return;
            }

            if (!DoesActivationMatchFocus(eventData))
            {
                return;
            }

            bool handInProximity = eventData.NewFocusedObject != null && eventData.NewFocusedObject.transform.IsChildOf(transform);
            if (handInProximity == wireframeOnly)
            {
                wireframeOnly = !handInProximity;
                ResetHandleVisibility();
            }
        }

        void IMixedRealityFocusHandler.OnFocusExit(FocusEventData eventData)
        {
            if (currentPointer != null && eventData.Pointer == currentPointer)
            {
                DropController();
            }
        }

        void IMixedRealityFocusHandler.OnFocusEnter(FocusEventData eventData) { }

        private void OnPointerUp(MixedRealityPointerEventData eventData)
        {
            if (currentPointer != null && eventData.Pointer == currentPointer)
            {
                DropController();
                eventData.Use();
            }
        }

        private void OnPointerDown(MixedRealityPointerEventData eventData)
        {
            if (currentPointer == null && !eventData.used)
            {
                GameObject grabbedHandle = eventData.Pointer.Result.CurrentPointerTarget;
                Transform grabbedHandleTransform = grabbedHandle.transform;
                currentHandleType = GetHandleType(grabbedHandleTransform);
                if (currentHandleType != HandleType.None)
                {
                    currentPointer = eventData.Pointer;
                    initialGrabPoint = currentPointer.Result.Details.Point;
                    currentGrabPoint = initialGrabPoint;
                    initialScaleOnGrabStart = boundingBoxTarget.Target.transform.localScale;
                    initialPositionOnGrabStart = boundingBoxTarget.Target.transform.position;
                    grabPointInPointer = Quaternion.Inverse(eventData.Pointer.Rotation) * (initialGrabPoint - currentPointer.Position);

                    SetHighlighted(grabbedHandleTransform);

                    if (currentHandleType == HandleType.Scale)
                    {
                        // Will use this to scale the target relative to the opposite corner
                        oppositeCorner = rigRoot.transform.TransformPoint(-grabbedHandle.transform.localPosition);
                        diagonalDir = (grabbedHandle.transform.position - oppositeCorner).normalized;

                        ScaleStarted?.Invoke();

                        if (debugText != null)
                        {
                            debugText.text = "OnPointerDown:ScaleStarted";
                        }
                    }
                    else if (currentHandleType == HandleType.Rotation)
                    {
                        currentRotationAxis = GetRotationAxis(grabbedHandleTransform);

                        RotateStarted?.Invoke();

                        if (debugText != null)
                        {
                            debugText.text = "OnPointerDown:RotateStarted";
                        }
                    }

                    eventData.Use();
                }
            }

            if (currentPointer != null)
            {
                // Always mark the pointer data as used to prevent any other behavior to handle pointer events
                // as long as BoundingBox manipulation is active.
                // This is due to us reacting to both "Select" and "Grip" events.
                eventData.Use();
            }
        }

        private void OnPointerDragged(MixedRealityPointerEventData eventData) { }

        public void OnSourceDetected(SourceStateEventData eventData)
        {
            if (eventData.Controller != null)
            {
                if (sourcesDetected.Count == 0 || sourcesDetected.Contains(eventData.Controller) == false)
                {
                    sourcesDetected.Add(eventData.Controller);
                }
            }
        }

        public void OnSourceLost(SourceStateEventData eventData)
        {
            sourcesDetected.Remove(eventData.Controller);

            if (currentPointer != null && currentPointer.InputSourceParent.SourceId == eventData.SourceId)
            {
                HandleType lastHandleType = currentHandleType;

                currentPointer = null;
                currentHandleType = HandleType.None;
                ResetHandleVisibility();

                if (lastHandleType == HandleType.Scale)
                {
                    if (debugText != null) debugText.text = "OnSourceLost:ScaleStopped";
                    ScaleStopped?.Invoke();
                }
                else if (lastHandleType == HandleType.Rotation)
                {
                    if (debugText != null) debugText.text = "OnSourceLost:RotateStopped";
                    RotateStopped?.Invoke();
                }
            }
        }

        #endregion Used Event Handlers


        #region Unused Event Handlers

        void IMixedRealityFocusChangedHandler.OnBeforeFocusChange(FocusEventData eventData) { }

        #endregion Unused Event Handlers



        #region Enums

        /// <summary>
        /// This enum defines which of the axes a given rotation handle revolves about.
        /// </summary>
        public enum CardinalAxisType
        {
            X = 0,
            Y,
            Z
        }
       
       

        #endregion Enums

       





        

        private List<Transform> links;
        private List<Renderer> linkRenderers;



        /// <summary>
        /// Returns list of transforms pointing to the scale handles of the bounding box.
        /// </summary>
        public IReadOnlyList<Transform> ScaleCorners
        {
            get { return corners; }
        }



        /// <summary>
        /// Returns list of transforms pointing to the rotation handles of the bounding box.
        /// </summary>
        public IReadOnlyList<Transform> RotateMidpoints
        {
            get { return balls; }
        }

        private void DestroyRig()
        {
            if (boundsOverride == null)
            {
                Destroy(boundingBoxTarget.TargetBounds);
            }
            else
            {
                boundsOverride.size -= boxPadding;

                if (boundingBoxTarget.TargetBounds != null)
                {
                    if (boundingBoxTarget.TargetBounds.gameObject.GetComponent<NearInteractionGrabbable>())
                    {
                        Destroy(boundingBoxTarget.TargetBounds.gameObject.GetComponent<NearInteractionGrabbable>());
                    }
                }
            }

            proximityEffect.ClearHandles();

           

            if (links != null)
            {
                foreach (Transform transform in links)
                {
                    Destroy(transform.gameObject);
                }
                links.Clear();
                links = null;
            }

          
            scaleHandles.DestroyHandles();

            if (rigRoot != null)
            {
                Destroy(rigRoot.gameObject);
                rigRoot = null;
            }

        }

        private void UpdateRigVisibilityInInspector()
        {
            scaleHandles.UpdateVisibilityInInspector(hideElementsInInspector);

            HideFlags desiredFlags = hideElementsInInspector ? HideFlags.HideInHierarchy | HideFlags.HideInInspector : HideFlags.None;
          

            if (boxDisplay != null)
            {
                boxDisplay.hideFlags = desiredFlags;
            }

            if (rigRoot != null)
            {
                rigRoot.hideFlags = desiredFlags;
            }

            if (links != null)
            {
                foreach (var link in links)
                {
                    link.hideFlags = desiredFlags;
                }
            }
        }

        private Vector3 GetRotationAxis(Transform handle)
        {
            int rotationHandleIdx = scaleHandles.GetRotationHandleIdx(handle);
            if (rotationHandleIdx < edgeAxes.Length)
            {
                if (edgeAxes[rotationHandleIdx] == CardinalAxisType.X)
                {
                    return rigRoot.transform.right;
                }
                else if (edgeAxes[rotationHandleIdx] == CardinalAxisType.Y)
                {
                    return rigRoot.transform.up;
                }
                else
                {
                    return rigRoot.transform.forward;
                }
            }

            return Vector3.zero;
        }

        private void SetHighlighted(Transform activeHandle)
        {
            scaleHandles.SetHighlighted(activeHandle, handleGrabbedMaterial);

            //update the box material to the grabbed material
            if (boxDisplay != null)
            {
                ApplyMaterialToAllRenderers(boxDisplay, boxGrabbedMaterial);
            }
        }


       

        private void Flatten()
        {
            if (flattenAxis == FlattenModeType.FlattenX)
            {
                flattenedHandles = new int[] { 0, 4, 2, 6 };
            }
            else if (flattenAxis == FlattenModeType.FlattenY)
            {
                flattenedHandles = new int[] { 1, 3, 5, 7 };
            }
            else if (flattenAxis == FlattenModeType.FlattenZ)
            {
                flattenedHandles = new int[] { 9, 10, 8, 11 };
            }

            if (flattenedHandles != null && linkRenderers != null)
            {
                for (int i = 0; i < flattenedHandles.Length; ++i)
                {
                    linkRenderers[flattenedHandles[i]].enabled = false;
                }
            }
        }

        private void SetHiddenHandles()
        {
            if (flattenedHandles != null)
            {
                for (int i = 0; i < flattenedHandles.Length; ++i)
                {
                    balls[flattenedHandles[i]].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Make the handle colliders ignore specified collider. (e.g. spatial mapping's floor collider to avoid the object get lifted up)
        /// </summary>
        private void HandleIgnoreCollider()
        {
            if (handlesIgnoreCollider != null)
            {
                scaleHandles.HandleIgnoreCollider(handlesIgnoreCollider);

                foreach (Transform ball in balls)
                {
                    Collider[] colliders = ball.gameObject.GetComponents<Collider>();
                    foreach (Collider collider in colliders)
                    {
                        UnityEngine.Physics.IgnoreCollision(collider, handlesIgnoreCollider);
                    }
                }
            }
        }

        private void SetMaterials()
        {
            //ensure materials
            if (wireframeMaterial == null)
            {
                float[] color = { 1.0f, 1.0f, 1.0f, 0.75f };

                Shader shader = Shader.Find("Mixed Reality Toolkit/Standard");

                wireframeMaterial = new Material(shader);
                wireframeMaterial.EnableKeyword("_InnerGlow");
                wireframeMaterial.SetColor("_Color", new Color(0.0f, 0.63f, 1.0f));
                wireframeMaterial.SetFloat("_InnerGlow", 1.0f);
                wireframeMaterial.SetFloatArray("_InnerGlowColor", color);
            }
            if (handleMaterial == null && handleMaterial != wireframeMaterial)
            {
                float[] color = { 1.0f, 1.0f, 1.0f, 0.75f };

                Shader shader = Shader.Find("Mixed Reality Toolkit/Standard");

                handleMaterial = new Material(shader);
                handleMaterial.EnableKeyword("_InnerGlow");
                handleMaterial.SetColor("_Color", new Color(0.0f, 0.63f, 1.0f));
                handleMaterial.SetFloat("_InnerGlow", 1.0f);
                handleMaterial.SetFloatArray("_InnerGlowColor", color);
            }
            if (handleGrabbedMaterial == null && handleGrabbedMaterial != handleMaterial && handleGrabbedMaterial != wireframeMaterial)
            {
                float[] color = { 1.0f, 1.0f, 1.0f, 0.75f };

                Shader shader = Shader.Find("Mixed Reality Toolkit/Standard");

                handleGrabbedMaterial = new Material(shader);
                handleGrabbedMaterial.EnableKeyword("_InnerGlow");
                handleGrabbedMaterial.SetColor("_Color", new Color(0.0f, 0.63f, 1.0f));
                handleGrabbedMaterial.SetFloat("_InnerGlow", 1.0f);
                handleGrabbedMaterial.SetFloatArray("_InnerGlowColor", color);
            }
        }

        private void InitializeDataStructures()
        {
            //boundsCorners = new Vector3[8];

            //corners = new List<Transform>();
            balls = new List<Transform>();

           // handles = new List<Handle>();

            if (showWireframe)
            {
                links = new List<Transform>();
                linkRenderers = new List<Renderer>();
            }

            sourcesDetected = new List<IMixedRealityController>();
        }



        private void ResetHandleVisibility()
        {
            if (currentPointer != null)
            {
                return;
            }

            bool isVisible;

            //set balls visibility
            if (balls != null)
            {
                isVisible = (active == true && wireframeOnly == false);
                for (int i = 0; i < balls.Count; ++i)
                {
                    balls[i].gameObject.SetActive(isVisible && ShouldRotateHandleBeVisible(edgeAxes[i]));
                    ApplyMaterialToAllRenderers(balls[i].gameObject, handleMaterial);
                }
            }

            //set link visibility
            if (links != null)
            {
                isVisible = active == true;
                for (int i = 0; i < linkRenderers.Count; ++i)
                {
                    if (linkRenderers[i] != null)
                    {
                        linkRenderers[i].enabled = isVisible;
                    }
                }
            }

            //set box display visibility
            if (boxDisplay != null)
            {
                boxDisplay.SetActive(active);
                ApplyMaterialToAllRenderers(boxDisplay, boxMaterial);
            }

            isVisible = (active == true && wireframeOnly == false && showScaleHandles == true);
            scaleHandles.ResetHandleVisibility(isVisible);

            SetHiddenHandles();
        }


        public static void ApplyMaterialToAllRenderers(GameObject root, Material material)
        {
            if (material != null)
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

                for (int i = 0; i < renderers.Length; ++i)
                {
                    renderers[i].material = material;
                }
            }
        }


        /// <summary>
        /// Destroys and re-creates the rig around the bounding box
        /// </summary>
        public void CreateRig()
        {
            DestroyRig();
            SetMaterials();
            InitializeRigRoot();
            InitializeDataStructures();
            SetBoundingBoxCollider();
            UpdateBounds();
            AddCorners();
            AddLinks();
            HandleIgnoreCollider();
            AddBoxDisplay();
            UpdateRigHandles();
            Flatten();
            ResetHandleVisibility();
            rigRoot.gameObject.SetActive(active);
            UpdateRigVisibilityInInspector();
        }


        private void InitializeRigRoot()
        {
            var rigRootObj = new GameObject(rigRootName);
            rigRoot = rigRootObj.transform;
            rigRoot.parent = transform;

            var pH = rigRootObj.AddComponent<PointerHandler>();
            pH.OnPointerDown.AddListener(OnPointerDown);
            pH.OnPointerDragged.AddListener(OnPointerDragged);
            pH.OnPointerUp.AddListener(OnPointerUp);
        }

        private void UpdateRigHandles()
        {
            if (rigRoot != null && boundingBoxTarget.Target != null)
            {
                // We move the rigRoot to the scene root to ensure that non-uniform scaling performed
                // anywhere above the rigRoot does not impact the position of rig corners / edges
                rigRoot.parent = null;

                rigRoot.rotation = Quaternion.identity;
                rigRoot.position = Vector3.zero;
                rigRoot.localScale = Vector3.one;

                scaleHandles.UpdateRigHandles();

                Vector3 rootScale = rigRoot.lossyScale;
                Vector3 invRootScale = new Vector3(1.0f / rootScale[0], 1.0f / rootScale[1], 1.0f / rootScale[2]);

                // Compute the local scale that produces the desired world space dimensions
                Vector3 linkDimensions = Vector3.Scale(GetLinkDimensions(), invRootScale);

                for (int i = 0; i < edgeCenters.Length; ++i)
                {
                    balls[i].position = edgeCenters[i];

                    if (links != null)
                    {
                        links[i].position = edgeCenters[i];

                        if (edgeAxes[i] == CardinalAxisType.X)
                        {
                            links[i].localScale = new Vector3(wireframeEdgeRadius, linkDimensions.x, wireframeEdgeRadius);
                        }
                        else if (edgeAxes[i] == CardinalAxisType.Y)
                        {
                            links[i].localScale = new Vector3(wireframeEdgeRadius, linkDimensions.y, wireframeEdgeRadius);
                        }
                        else//Z
                        {
                            links[i].localScale = new Vector3(wireframeEdgeRadius, linkDimensions.z, wireframeEdgeRadius);
                        }
                    }
                }

                if (boxDisplay != null)
                {
                    // Compute the local scale that produces the desired world space size
                    boxDisplay.transform.localScale = Vector3.Scale(GetBoxDisplayScale(), invRootScale);
                }

                //move rig into position and rotation
                rigRoot.position = boundingBoxTarget.TargetBounds.bounds.center;
                rigRoot.rotation = boundingBoxTarget.Target.transform.rotation;
                rigRoot.parent = transform;
            }
        }


        /// <summary>
        /// Allows to manually enable wire (edge) highlighting (edges) of the bounding box.
        /// This is useful if connected to the Manipulation events of a
        /// <see cref="Microsoft.MixedReality.Toolkit.UI.ManipulationHandler"/> 
        /// when used in conjunction with this MonoBehavior.
        /// </summary>
        public void HighlightWires()
        {
            SetHighlighted(null);
        }

        public void UnhighlightWires()
        {
            ResetHandleVisibility();
        }



        


        private void AddLinks()
        {

            /*edges.*/InitEdges(boundsCorners);

            if (links != null)
            {
                GameObject link;
                for (int i = 0; i < edgeCenters.Length; ++i)
                {
                    if (wireframeShape == WireframeType.Cubic)
                    {
                        link = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Destroy(link.GetComponent<BoxCollider>());
                    }
                    else
                    {
                        link = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        Destroy(link.GetComponent<CapsuleCollider>());
                    }
                    link.name = "link_" + i.ToString();


                    Vector3 linkDimensions = GetLinkDimensions();
                    if (edgeAxes[i] == CardinalAxisType.Y)
                    {
                        link.transform.localScale = new Vector3(wireframeEdgeRadius, linkDimensions.y, wireframeEdgeRadius);
                        link.transform.Rotate(new Vector3(0.0f, 90.0f, 0.0f));
                    }
                    else if (edgeAxes[i] == CardinalAxisType.Z)
                    {
                        link.transform.localScale = new Vector3(wireframeEdgeRadius, linkDimensions.z, wireframeEdgeRadius);
                        link.transform.Rotate(new Vector3(90.0f, 0.0f, 0.0f));
                    }
                    else//X
                    {
                        link.transform.localScale = new Vector3(wireframeEdgeRadius, linkDimensions.x, wireframeEdgeRadius);
                        link.transform.Rotate(new Vector3(0.0f, 0.0f, 90.0f));
                    }

                    link.transform.position = edgeCenters[i];
                    link.transform.parent = rigRoot.transform;
                    Renderer linkRenderer = link.GetComponent<Renderer>();
                    linkRenderers.Add(linkRenderer);

                    if (wireframeMaterial != null)
                    {
                        linkRenderer.material = wireframeMaterial;
                    }

                    links.Add(link.transform);
                }
            }
        }



        private void AddBoxDisplay()
        {
            if (boxMaterial != null)
            {
                bool isFlattened = flattenAxis != FlattenModeType.DoNotFlatten;

                boxDisplay = GameObject.CreatePrimitive(isFlattened ? PrimitiveType.Quad : PrimitiveType.Cube);
                Destroy(boxDisplay.GetComponent<Collider>());
                boxDisplay.name = "bounding box";

                ApplyMaterialToAllRenderers(boxDisplay, boxMaterial);

                boxDisplay.transform.localScale = GetBoxDisplayScale();
                boxDisplay.transform.parent = rigRoot.transform;
            }
        }

        private Vector3 GetBoxDisplayScale()
        {
            // When a box is flattened one axis is normally scaled to zero, this doesn't always work well with visuals so we take 
            // that flattened axis and re-scale it to the flattenAxisDisplayScale.
            Vector3 displayScale = currentBoundsExtents;
            displayScale.x = (flattenAxis == FlattenModeType.FlattenX) ? flattenAxisDisplayScale : displayScale.x;
            displayScale.y = (flattenAxis == FlattenModeType.FlattenY) ? flattenAxisDisplayScale : displayScale.y;
            displayScale.z = (flattenAxis == FlattenModeType.FlattenZ) ? flattenAxisDisplayScale : displayScale.z;

            return 2.0f * displayScale;
        }




        private bool ShouldRotateHandleBeVisible(CardinalAxisType axisType)
        {
            return
                (axisType == CardinalAxisType.X && showRotationHandleForX) ||
                (axisType == CardinalAxisType.Y && showRotationHandleForY) ||
                (axisType == CardinalAxisType.Z && showRotationHandleForZ);
        }
















        private Vector3[] edgeCenters;
        private CardinalAxisType[] edgeAxes;


        public void InitEdges(Vector3[] boundsCorners)
        {
            edgeCenters = new Vector3[12];
            CalculateEdgeCenters(boundsCorners);
            InitEdgeAxis();
            AdMidPointVisuals();
        }

        private Vector3 GetLinkDimensions()
        {
            float linkLengthAdjustor = wireframeShape == WireframeType.Cubic ? 2.0f : 1.0f - (6.0f * wireframeEdgeRadius);
            return (currentBoundsExtents * linkLengthAdjustor) + new Vector3(wireframeEdgeRadius, wireframeEdgeRadius, wireframeEdgeRadius);
        }


        private void CalculateEdgeCenters(Vector3[] boundsCorners)
        {
            if (boundsCorners != null && edgeCenters != null)
            {
                edgeCenters[0] = (boundsCorners[0] + boundsCorners[1]) * 0.5f;
                edgeCenters[1] = (boundsCorners[0] + boundsCorners[2]) * 0.5f;
                edgeCenters[2] = (boundsCorners[3] + boundsCorners[2]) * 0.5f;
                edgeCenters[3] = (boundsCorners[3] + boundsCorners[1]) * 0.5f;

                edgeCenters[4] = (boundsCorners[4] + boundsCorners[5]) * 0.5f;
                edgeCenters[5] = (boundsCorners[4] + boundsCorners[6]) * 0.5f;
                edgeCenters[6] = (boundsCorners[7] + boundsCorners[6]) * 0.5f;
                edgeCenters[7] = (boundsCorners[7] + boundsCorners[5]) * 0.5f;

                edgeCenters[8] = (boundsCorners[0] + boundsCorners[4]) * 0.5f;
                edgeCenters[9] = (boundsCorners[1] + boundsCorners[5]) * 0.5f;
                edgeCenters[10] = (boundsCorners[2] + boundsCorners[6]) * 0.5f;
                edgeCenters[11] = (boundsCorners[3] + boundsCorners[7]) * 0.5f;
            }
        }

        public void InitEdgeAxis()
        {

            edgeAxes = new CardinalAxisType[12];
            edgeAxes[0] = CardinalAxisType.X;
            edgeAxes[1] = CardinalAxisType.Y;
            edgeAxes[2] = CardinalAxisType.X;
            edgeAxes[3] = CardinalAxisType.Y;
            edgeAxes[4] = CardinalAxisType.X;
            edgeAxes[5] = CardinalAxisType.Y;
            edgeAxes[6] = CardinalAxisType.X;
            edgeAxes[7] = CardinalAxisType.Y;
            edgeAxes[8] = CardinalAxisType.Z;
            edgeAxes[9] = CardinalAxisType.Z;
            edgeAxes[10] = CardinalAxisType.Z;
            edgeAxes[11] = CardinalAxisType.Z;
        }

        public void AdMidPointVisuals()
        {

            for (int i = 0; i < edgeCenters.Length; ++i)
            {
                GameObject midpoint = new GameObject();
                midpoint.name = "midpoint_" + i.ToString();
                midpoint.transform.position = edgeCenters[i];
                midpoint.transform.parent = rigRoot.transform;

                GameObject midpointVisual;
                if (rotationHandlePrefab != null)
                {
                    midpointVisual = Instantiate(rotationHandlePrefab);
                }
                else
                {
                    midpointVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(midpointVisual.GetComponent<SphereCollider>());
                }

                // Align handle with its edge assuming that the prefab is initially aligned with the up direction 
                if (edgeAxes[i] == CardinalAxisType.X)
                {
                    Quaternion realignment = Quaternion.FromToRotation(Vector3.up, Vector3.right);
                    midpointVisual.transform.localRotation = realignment * midpointVisual.transform.localRotation;
                }
                else if (edgeAxes[i] == CardinalAxisType.Z)
                {
                    Quaternion realignment = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                    midpointVisual.transform.localRotation = realignment * midpointVisual.transform.localRotation;
                }

                Bounds midpointBounds = GetMaxBounds(midpointVisual);
                float maxDim = Mathf.Max(
                    Mathf.Max(midpointBounds.size.x, midpointBounds.size.y),
                    midpointBounds.size.z);
                float invScale = rotationHandleSize / maxDim;

                midpointVisual.transform.parent = midpoint.transform;
                midpointVisual.transform.localScale = new Vector3(invScale, invScale, invScale);
                midpointVisual.transform.localPosition = Vector3.zero;

                AddComponentsToAffordance(midpoint, new Bounds(midpointBounds.center * invScale, midpointBounds.size * invScale), rotationHandlePrefabColliderType, CursorContextInfo.CursorAction.Rotate, rotateHandleColliderPadding);

                balls.Add(midpoint.transform);

                proximityEffect.AddHandle(HandleType.Rotation, midpointVisual);

               

                if (handleMaterial != null)
                {
                    ApplyMaterialToAllRenderers(midpointVisual, handleMaterial);
                }
            }

        }









        public void TransformTarget(HandleType transformType)
        {
            if (transformType != HandleType.None)
            {
                Vector3 prevGrabPoint = currentGrabPoint;
                currentGrabPoint = (currentPointer.Rotation * grabPointInPointer) + currentPointer.Position;

                if (transformType == HandleType.Rotation)
                {
                    Vector3 prevDir = Vector3.ProjectOnPlane(prevGrabPoint - rigRoot.transform.position, currentRotationAxis).normalized;
                    Vector3 currentDir = Vector3.ProjectOnPlane(currentGrabPoint - rigRoot.transform.position, currentRotationAxis).normalized;
                    Quaternion q = Quaternion.FromToRotation(prevDir, currentDir);
                    q.ToAngleAxis(out float angle, out Vector3 axis);

                    Target.transform.RotateAround(rigRoot.transform.position, axis, angle);
                }
                else if (transformType == HandleType.Scale)
                {
                    float initialDist = Vector3.Dot(initialGrabPoint - oppositeCorner, diagonalDir);
                    float currentDist = Vector3.Dot(currentGrabPoint - oppositeCorner, diagonalDir);
                    float scaleFactor = 1 + (currentDist - initialDist) / initialDist;

                    Vector3 newScale = initialScaleOnGrabStart * scaleFactor;
                    Vector3 clampedScale = newScale;
                    if (scaleHandler != null)
                    {
                        clampedScale = scaleHandler.ClampScale(newScale);
                        if (clampedScale != newScale)
                        {
                            scaleFactor = clampedScale[0] / initialScaleOnGrabStart[0];
                        }
                    }

                    Target.transform.localScale = clampedScale;
                    Target.transform.position = initialPositionOnGrabStart * scaleFactor + (1 - scaleFactor) * oppositeCorner;
                }
            }
        }

    }
}
