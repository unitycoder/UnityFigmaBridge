using System.Runtime.InteropServices.ComTypes;
using Mono.Cecil;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UnityFigmaBridge.Runtime.UI
{
    /// <summary>
    /// Figma Image UI Component. Used in conjunction with FigmaImageShader to render Figma components with appropriate
    /// settings
    /// </summary>
    public class FigmaImage : Image
    {


        /// <summary>
        /// How the stroke is rendered relative to the edge of the shape 
        /// </summary>
        public enum StrokePositionType
        {
            Inside,
            Outside,
            Center
        }

        /// <summary>
        /// Fill style of the shape
        /// </summary>
        public enum FillStyle
        {
            Solid,
            LinearGradient,
            RadialGradient
        }

        /// <summary>
        /// The shape type to render
        /// </summary>
        public enum ShapeType
        {
            Rectangle,
            Ellipse,
            Star
        }

        public enum ImageScaleMode
        {
            Fill,
            Fit,
            Stretch,
            Tile
        }

        [SerializeField] protected ShapeType m_Shape = ShapeType.Rectangle;
        [SerializeField] protected Color m_StrokeColor = Color.white;
        [SerializeField] protected Color m_FillColor = Color.white;
        [SerializeField] protected float m_StrokeWidth = 0;
        [SerializeField] protected Vector4 m_CornerRadius;
        [SerializeField] protected StrokePositionType m_StrokePosition=StrokePositionType.Inside;
        [SerializeField] protected FillStyle m_Fill = FillStyle.Solid;
        [SerializeField] protected ImageScaleMode m_ImageScaleMode = ImageScaleMode.Fill;
        [SerializeField] protected float m_EllipseInnerRadius = 1;
        [SerializeField] protected Vector2 m_EllipseArcAngleRange = new Vector2(0,Mathf.PI*2.0f);
        
        /// <summary>
        /// Transform data for image fill
        /// </summary>
        [SerializeField] protected Vector3[] m_ImageTransform;

        /// <summary>
        /// Image scale factor, determines percentage scale of image fill in tile mode
        /// </summary>
        [SerializeField] protected float m_ImageScaleFactor = 1;
        // The gradient to apply
        [SerializeField] protected Gradient m_FillGradient;
        // The two normalised positions that affect the gradient position and rotation- https://www.figma.com/widget-docs/api/type-GradientPaint/
        // Default to linear horizontal
        [SerializeField] protected Vector4 m_GradientHandlePositions=new Vector4(0,0,1,0);
        
        private static readonly int s_StrokeColorPropertyID = Shader.PropertyToID("_StrokeColor");
        private static readonly int s_FillColorPropertyID = Shader.PropertyToID("_FillColor");
        private static readonly int s_StrokeWidthPropertyID = Shader.PropertyToID("_StrokeWidth");
        private static readonly int s_CornerRadiusPropertyID = Shader.PropertyToID("_CornerRadius");
        private static readonly int s_GradientColorsPropertyID = Shader.PropertyToID("_GradientColors");
        private static readonly int s_GradientStopsPropertyID = Shader.PropertyToID("_GradientStops");
        private static readonly int s_GradientHandlePositionsPropertyID = Shader.PropertyToID("_GradientHandlePositions");
        private static readonly int s_GradientNumStopsPropertyID = Shader.PropertyToID("_GradientNumStops");
        private static readonly int s_ArcAngleRangeInnerRadiusPropertyID = Shader.PropertyToID("_ArcAngleRangeInnerRadius");
        

        private const string FIGMA_SHADER_NAME = "Figma/FigmaImageShader";
        private Material m_DynamicMaterial;

        private const int MAX_GRADIENT_STOPS = 16;
        /// <summary>
        /// We want to check that the canvas has the right channels to display
        /// </summary>
        private bool m_ConfirmedAdditionalCanvasChannels=false;

        protected override void OnEnable()
        {
            EnsureCanvasHasChannelsForFigmaImage();
            base.OnEnable();
        }

        public ShapeType Shape
        {
            get => m_Shape;
            set
            {
                m_Shape = value;
                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// Color of the Shape Stroke
        /// </summary>
        public Color StrokeColor
        {
            get => m_StrokeColor;
            set
            {
                m_StrokeColor = value;
                material.SetColor(s_StrokeColorPropertyID, m_StrokeColor);
                base.SetMaterialDirty();
            }
        }
        
        public Color FillColor
        {
            get => m_FillColor;
            set
            {
                m_FillColor = value;
                material.SetColor(s_FillColorPropertyID, m_FillColor);
                base.SetMaterialDirty();
            }
        }
        

        /// <summary>
        /// Width of the Shape Stroke
        /// </summary>
        public float StrokeWidth
        {
            get => m_StrokeWidth;
            set
            {
                m_StrokeWidth = value;
                material.SetFloat(s_StrokeWidthPropertyID, m_StrokeWidth);
                base.SetMaterialDirty();
            }
        }
        
        /// <summary>
        /// Corner radius for each corner of the rectangle
        /// </summary>
        public Vector4 CornerRadius
        {
            get => m_CornerRadius;
            set
            {
                m_CornerRadius = value;
                material.SetVector(s_CornerRadiusPropertyID, m_CornerRadius);
                base.SetMaterialDirty();
            }
        }

        public StrokePositionType StrokePosition
        {
            get => m_StrokePosition;
            set
            {
                m_StrokePosition = value;
                base.SetMaterialDirty();
            }
        }
        
        public FillStyle Fill
        {
            get => m_Fill;
            set
            {
                m_Fill = value;
                base.SetMaterialDirty();
            }
        }

        public ImageScaleMode ScaleMode
        {
            get => m_ImageScaleMode;
            set
            {
                m_ImageScaleMode = value;
                base.SetMaterialDirty();
            }
        }
        
        public Gradient FillGradient
        {
            get => m_FillGradient;
            set
            {
                m_FillGradient = value;
                base.SetMaterialDirty();
            }
        }
        
        public Vector4 GradientHandlePositions
        {
            get => m_GradientHandlePositions;
            set
            {
                m_GradientHandlePositions = value;
                base.SetMaterialDirty();
            }
        }

        public Vector3[] ImageTransform
        {
            get => m_ImageTransform;
            set
            {
                m_ImageTransform = value;
                base.SetMaterialDirty();
            }
        }

        public float ImageScaleFactor
        {
            get => m_ImageScaleFactor;
            set
            {
                m_ImageScaleFactor = value;
                base.SetMaterialDirty();
            }
        }
        
        /// <summary>
        ///  Defines ratio of ellipse (allowing inner section to be removed)
        /// </summary>
        public float EllipseInnerRadius
        {
            get => m_EllipseInnerRadius;
            set
            {
                m_EllipseInnerRadius = value;
                base.SetMaterialDirty();
            }
        }
        
        /// <summary>
        ///  Defines ratio of ellipse (allowing inner section to be removed)
        /// </summary>
        public Vector2 EllipseArcAngleRange
        {
            get => m_EllipseArcAngleRange;
            set
            {
                m_EllipseArcAngleRange = value;
                base.SetMaterialDirty();
            }
        }

        /// <summary>
        /// Callback function when a UI element needs to generate vertices. Fills the vertex buffer data.
        /// </summary>
        /// <param name="vh">VertexHelper utility.</param>
        /// <remarks>
        /// Used by Text, UI.Image, and RawImage for example to generate vertices specific to their use case.
        /// </remarks>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            // Before rendering, ensure we have already set channels. This is also required when inspecting a prefab
            EnsureCanvasHasChannelsForFigmaImage();
            
            var r = GetPixelAdjustedRect();

            var v = new Vector4(r.x, r.y, r.x + r.width, r.y + r.height);

            // We'll need to know size to calculate SDF - Store in UV
            Vector2 rtSize = new Vector2(r.width, r.height);

            // TODO - Remove workaround
            // For perfectly ellipse, we get issues in shader, so adjust
            if (m_Shape == ShapeType.Ellipse && Mathf.Abs(r.width - r.height) < 0.001f) rtSize.x += 0.001f;

            Vector2[] texCoords;
            
            
            switch (m_ImageScaleMode)
            {
                case ImageScaleMode.Stretch:
                    // Crop
                    texCoords=new Vector2[]
                    {
                        new Vector2(m_ImageTransform[0].z, 1.0f - m_ImageTransform[1].z - m_ImageTransform[1].y),
                        new Vector2(m_ImageTransform[0].z, 1.0f - m_ImageTransform[1].z),
                        new Vector2(m_ImageTransform[0].z + m_ImageTransform[0].x, 1.0f - m_ImageTransform[1].z),
                        new Vector2(m_ImageTransform[0].z + m_ImageTransform[0].x,
                            1.0f - m_ImageTransform[1].z - m_ImageTransform[1].y),
                    };
                    break;
                case ImageScaleMode.Tile:
                    var renderWidth = sprite.rect.width*m_ImageScaleFactor;
                    var renderHeight = sprite.rect.height*m_ImageScaleFactor;
                    var tileFactor = new Vector2(r.width / renderWidth, r.height / renderHeight);
                    texCoords=new Vector2[]
                    {
                        new Vector2(0, 1-tileFactor.y),
                        new Vector2(0f, 1),
                        new Vector2(tileFactor.x, 1),
                        new Vector2(tileFactor.x, 1-tileFactor.y)
                    };
                    break;
                default:
                    texCoords=new Vector2[]
                    {
                        new Vector2(0, 0),
                        new Vector2(0f, 1f),
                        new Vector2(1f, 1f),
                        new Vector2(1f, 0f)
                    };
                    break;

            }
            
            // Largely this is the the same as Graphic original, but with extra UV Channels settings
            Color32 color32 = color;
            vh.Clear();
            // Order is TL, BL, TR, BR
            vh.AddVert(new Vector3(v.x, v.y), color32, texCoords[0],new Vector4(0f, 0,rtSize.x,rtSize.y), Vector3.zero, Vector4.zero);
            vh.AddVert(new Vector3(v.x, v.w), color32,texCoords[1], new Vector4(0f, 1f, rtSize.x,rtSize.y), Vector3.zero, Vector4.zero);
            vh.AddVert(new Vector3(v.z, v.w), color32, texCoords[2],new Vector4(1f, 1f,rtSize.x,rtSize.y), Vector3.zero, Vector4.zero);
            vh.AddVert(new Vector3(v.z, v.y), color32, texCoords[3],new Vector4(1f, 0f,rtSize.x,rtSize.y), Vector3.zero, Vector4.zero);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

       
        /// <summary>
        /// Material tp use
        /// </summary>
        public override Material material => m_DynamicMaterial==null ? CreateDynamicMaterial() : m_DynamicMaterial;

        /// <summary>
        /// Create a new material using Figma Image Shader
        /// </summary>
        /// <returns></returns>
        private Material CreateDynamicMaterial()
        {
            return m_DynamicMaterial = new Material(Shader.Find(FIGMA_SHADER_NAME));
        }

        /// <summary>
        /// Apply settings to material
        /// </summary>
        /// <param name="baseMaterial"></param>
        /// <returns></returns>
        public override Material GetModifiedMaterial(Material baseMaterial)
        {
            var mat = base.GetModifiedMaterial(baseMaterial);
            // Set properties
            mat.SetColor(s_StrokeColorPropertyID, m_StrokeColor);
            mat.SetFloat(s_StrokeWidthPropertyID, m_StrokeWidth);
            mat.SetColor(s_FillColorPropertyID,m_FillColor);
            // Encode limits on arc angle range and inner radius
            mat.SetVector(s_ArcAngleRangeInnerRadiusPropertyID,new Vector4(m_EllipseArcAngleRange.x,m_EllipseArcAngleRange.y,m_EllipseInnerRadius,0));
            
            // We need to change order of the corners from Figma (TL/TR/BR/BL) to shader (TR/BR/TL/BL)
            mat.SetVector(s_CornerRadiusPropertyID,new Vector4(m_CornerRadius.y,m_CornerRadius.z,m_CornerRadius.x,m_CornerRadius.w));
            
            // Enable features depending on settings
            mat.SetKeyword(new LocalKeyword(baseMaterial.shader, "STROKE"),m_StrokeWidth > 0);
            
            mat.SetKeyword(new LocalKeyword(baseMaterial.shader, "LINEAR_GRADIENT"),m_Fill== FillStyle.LinearGradient);
            mat.SetKeyword(new LocalKeyword(baseMaterial.shader, "RADIAL_GRADIENT"),m_Fill== FillStyle.RadialGradient);
            
            mat.SetKeyword(new LocalKeyword(baseMaterial.shader, "SHAPE_RECTANGLE"),m_Shape== ShapeType.Rectangle);
            mat.SetKeyword(new LocalKeyword(baseMaterial.shader, "SHAPE_ELLIPSE"),m_Shape== ShapeType.Ellipse);
            mat.SetKeyword(new LocalKeyword(baseMaterial.shader, "SHAPE_STAR"),m_Shape== ShapeType.Star);
            
            // Second element is fill angle
            mat.SetKeyword(new LocalKeyword(baseMaterial.shader, "ARC_ANGLE_RANGE"),m_EllipseArcAngleRange.y<Mathf.PI*2.0f);
           
            // Set gradient properties if required
            switch (m_Fill)
            {
                case FillStyle.LinearGradient:
                case FillStyle.RadialGradient:
                    SetGradientProperties(mat);
                    break;
            }
            return mat;
        }

        /// <summary>
        /// To supp
        /// </summary>
        /// <param name="mat"></param>
        private void SetGradientProperties(Material mat)
        {
            var gradientStopCount = m_FillGradient.colorKeys.Length;
            var gradientColors = new Color[MAX_GRADIENT_STOPS];
            var gradientStops = new float[MAX_GRADIENT_STOPS];
            for (var i = 0; i < gradientStopCount; i++)
            {
                gradientColors[i] = m_FillGradient.colorKeys[i].color;
                gradientStops[i] = m_FillGradient.colorKeys[i].time;
                // Apply alpha to key by resampling to this position
                var percentGradientLength = i / (float)(i - 1);
                gradientColors[i].a= m_FillGradient.Evaluate(percentGradientLength).a;
            }
            mat.SetColorArray(s_GradientColorsPropertyID,gradientColors);
            mat.SetFloatArray(s_GradientStopsPropertyID,gradientStops);
            mat.SetFloat(s_GradientNumStopsPropertyID,gradientStopCount);
            mat.SetVector(s_GradientHandlePositionsPropertyID,m_GradientHandlePositions);
        }
        
        /// <summary>
        /// Ensure canvas has required additional channels to render Figma Image (we pass additional geometry through UV channels)
        /// </summary>
        /// <param name="parentCanvas">Canvas to add additional channels</param>
        private void EnsureCanvasHasChannelsForFigmaImage()
        {
            if (m_ConfirmedAdditionalCanvasChannels || canvas == null) return;
            AdditionalCanvasShaderChannels canvasAdditionalShaderChannels = canvas.additionalShaderChannels;
            canvasAdditionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            canvas.additionalShaderChannels = canvasAdditionalShaderChannels;
        }
        
    }




}
