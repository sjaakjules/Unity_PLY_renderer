using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.VFX;

public enum rendererType { CPU_Shuriken, GPU_PCX, GPU_VFX, GPU_PC }
public class PlyPointCloudData
{

    #region private variables
    bool _receivedData = false;
    bool _digestedData = false;
    bool _isReady = false;
    string _debugLog = "";
    rendererType _type;
    string _fileName;
    int _nPoints;

    DataBody _body;
    DataHeader _header;

    Texture2D _positionMap;
    Texture2D _colorMap;
    Texture2D _normalMap;

    ParticleSystem.Particle[] _particles;
    #endregion

    #region public accessors

    float _pointSize = .05f;
    public bool isFinished { get { return _isReady; } }

    public string name { get { return _fileName; } }

    public Texture2D getPositionMap { get { if (_isReady) return _positionMap; else return null; } }
    public Texture2D getColourMap { get { if (_isReady) return _colorMap; else return null; } }
    public Texture2D getNormalMap { get { if (_isReady) return _normalMap; else return null; } }

    public string debugLog { get { return _debugLog; } }

    public List<Vector3> getPositions { get { if (_isReady) { return _body.vertices; } return null; } }

    public List<Color32> getColours { get { if (_isReady) { return _body.colors; } return null; } }

    public List<Vector3> getNormals { get { if (_isReady) { return _body.normals; } return null; } }

    public float setPointSize { set { _pointSize = value; } }

    #endregion

    #region public methods
    public PlyPointCloudData(rendererType SelType)
    {
        this._type = SelType;
    }

    public IEnumerator GetRequest(string uri)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {

            _debugLog = "\nDownloading Pointcloud...\n";
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            _receivedData = true;
            string[] pages = uri.Split('/');
            int page = pages.Length - 1;

            if (webRequest.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.Log(pages[page] + ": Error: " + webRequest.error);
            }
            else
            {
                Debug.Log(pages[page] + ":\nReceived: " + webRequest.downloadHandler.text);
                if (webRequest.downloadHandler.isDone)
                {
                    string[] fileName = pages[page].Split('.');
                    _fileName = fileName[0];
                    _nPoints = ImportPointCloud(webRequest.downloadHandler.data, fileName[0]);
                }
            }

            _debugLog += "\nLoaded Pointcloud...\n";
            _isReady = true;
        }
    }

    public void updateRenderer(object renderer)
    {
        switch (_type)
        {
            case rendererType.CPU_Shuriken:
                ParticleSystem ps = (ParticleSystem)renderer;
                if (ps != null)
                {
                    if (_type != rendererType.CPU_Shuriken)
                    {
                        _type = rendererType.CPU_Shuriken;
                        Initialize(_body.vertices, _body.colors, _body.normals);
                    }
                    ParticleSystem.MainModule startSettigns = ps.main;
                    startSettigns.startSpeed = new ParticleSystem.MinMaxCurve(0);
                    startSettigns.maxParticles = _nPoints;
                    startSettigns.startLifetime = new ParticleSystem.MinMaxCurve(1000000);
                    startSettigns.duration = 1000000;

                    int rem_Particles = _nPoints - ps.particleCount;
                    if (rem_Particles < 0) rem_Particles = 0;
                    ps.Emit(rem_Particles);
                    ps.GetParticles(_particles);
                    if (_particles != null)
                    {
                        for (int i = 0; i < _particles.Length; i++)
                        {
                            _particles[i].position = getPositions[i];
                            _particles[i].startColor = getColours[i];
                            _particles[i].startSize = _pointSize;
                           // _particles[i].velocity = Vector3.zero;// normal[i];
                            _particles[i].rotation3D = Quaternion.FromToRotation(Vector3.up, getNormals[i]).eulerAngles;
                           // _particles[i].startLifetime = 100000;
                        }
                        //var shape = ps.shape;
                        //shape.enabled = true;
                        // shape.shapeType = ParticleSystemShapeType.Circle;
                        //shape.alignToDirection = true;
                        ps.SetParticles(_particles);
                        _debugLog += "Particles loaded to CPU renderer";
                    }
                    else
                    {
                        _debugLog += "no particles";
                    }
                }
                else _debugLog += "Renderer is null";
                break;
            case rendererType.GPU_PCX:

                PCXPointCloudRenderer pcxRenderer = (PCXPointCloudRenderer)renderer;
                if (pcxRenderer != null)
                {
                    if (_type != rendererType.GPU_PCX)
                    {
                        _type = rendererType.GPU_PCX;
                        Initialize(_body.vertices, _body.colors, _body.normals);
                    }
                    PCXPointCloudData data = new PCXPointCloudData();
                    data.Initialize(getPositions, getColours);
                    pcxRenderer.sourceData = data;
                    pcxRenderer.pointSize = _pointSize;
                    _debugLog += "Particles loaded to PCX renderer";
                }
                else _debugLog += "Renderer is null";
                break;
            case rendererType.GPU_VFX:
                VisualEffect vfxRenderer = (VisualEffect)renderer;

                if (vfxRenderer != null)
                {
                    if (_type != rendererType.GPU_VFX)
                    {
                        _type = rendererType.GPU_VFX;
                        Initialize(_body.vertices, _body.colors, _body.normals);
                    }
                    vfxRenderer.SetTexture("_Position", getPositionMap);
                    vfxRenderer.SetTexture("_Colour", getColourMap);
                    vfxRenderer.SetTexture("_Normal", getNormalMap);
                    vfxRenderer.SetFloat("_nPoints", _nPoints);
                    vfxRenderer.SetFloat("_PointSize", _pointSize);
                    vfxRenderer.Reinit();
                    _debugLog += "Particles loaded to VFX renderer";
                }
                else _debugLog += "Renderer is null";
                break;
            case rendererType.GPU_PC:
                _debugLog += "Unable to setup renderer.";
                break;
            default:
                _debugLog += "Unable to setup renderer.";
                break;
        }
    }

    #endregion

    #region private methods

    int ImportPointCloud(byte[] streamedData, string fileName)
    {
        try
        {
            MemoryStream stream = new MemoryStream(streamedData);
            _header = ReadDataHeader(new StreamReader(stream));
            _body = ReadDataBody(_header, new BinaryReader(stream));

            Initialize(_body.vertices, _body.colors, _body.normals);

            _digestedData = true;
            return _body.vertices.Count;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed importing " + fileName + ". \n" + e.Message);
        }
        return -1;
    }



    void Initialize(List<Vector3> positions, List<Color32> colors, List<Vector3> normal)
    {
        _nPoints = positions.Count;

        if (_type == rendererType.CPU_Shuriken)
        {
            _particles = new ParticleSystem.Particle[_nPoints];
            _debugLog += "\nAllocated memory for particle information...";
        }

        if (_type == rendererType.GPU_VFX)
        {
            var width = Mathf.CeilToInt(Mathf.Sqrt(_nPoints));

            _positionMap = new Texture2D(width, width, TextureFormat.RGBAHalf, false);
            _positionMap.name = "Position Map";
            _positionMap.filterMode = FilterMode.Point;

            _colorMap = new Texture2D(width, width, TextureFormat.RGBA32, false);
            _colorMap.name = "Color Map";
            _colorMap.filterMode = FilterMode.Point;

            _normalMap = new Texture2D(width, width, TextureFormat.RGBAHalf, false);
            _normalMap.name = "Normal Map";
            _normalMap.filterMode = FilterMode.Point;

            var i1 = 0;
            var i2 = 0U;

            for (var y = 0; y < width; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = i1 < _nPoints ? i1 : (int)(i2 % _nPoints);
                    var p = positions[i];
                    var n = normal[i];

                    _positionMap.SetPixel(x, y, new Color(p.x, p.y, p.z));
                    _colorMap.SetPixel(x, y, colors[i]);
                    _normalMap.SetPixel(x, y, new Color(n.x, n.y, n.z));

                    i1++;
                    i2 += 132049U; // prime
                }
            }

            _normalMap.Apply(false, true);
            _colorMap.Apply(false, true);
            _positionMap.Apply(false, true);
            _debugLog += "\nWritten textures to memory...";
        }

    }

    #endregion

    #region PLY reader 

    DataHeader ReadDataHeader(StreamReader reader)
    {
        var data = new DataHeader();
        var readCount = 0;

        // Magic number line ("ply")
        var line = reader.ReadLine();
        readCount += line.Length + 1;
        if (line != "ply")
            throw new ArgumentException("Magic number ('ply') mismatch.");

        // Data format: check if it's binary/little endian.
        line = reader.ReadLine();
        readCount += line.Length + 1;
        if (line != "format binary_little_endian 1.0")
            throw new ArgumentException(
                "Invalid data format ('" + line + "'). " +
                "Should be binary/little endian.");

        // Read header contents.
        for (var skip = false; ;)
        {
            // Read a line and split it with white space.
            line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line == "end_header") break;
            var col = line.Split();

            // Element declaration (unskippable)
            if (col[0] == "element")
            {
                if (col[1] == "vertex")
                {
                    data.vertexCount = Convert.ToInt32(col[2]);
                    skip = false;
                }
                else
                {
                    // Don't read elements other than vertices.
                    skip = true;
                }
            }

            if (skip) continue;

            // Property declaration line
            if (col[0] == "property")
            {
                var prop = DataProperty.Invalid;

                // Parse the property name entry.
                switch (col[2])
                {
                    case "red": prop = DataProperty.R8; break;
                    case "green": prop = DataProperty.G8; break;
                    case "blue": prop = DataProperty.B8; break;
                    case "alpha": prop = DataProperty.A8; break;
                    case "x": prop = DataProperty.SingleX; break;
                    case "y": prop = DataProperty.SingleY; break;
                    case "z": prop = DataProperty.SingleZ; break;
                    case "nx": prop = DataProperty.SingleNX; break;
                    case "ny": prop = DataProperty.SingleNY; break;
                    case "nz": prop = DataProperty.SingleNZ; break;
                }

                // Check the property type.
                if (col[1] == "char" || col[1] == "uchar" ||
                    col[1] == "int8" || col[1] == "uint8")
                {
                    if (prop == DataProperty.Invalid)
                        prop = DataProperty.Data8;
                    else if (GetPropertySize(prop) != 1)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else if (col[1] == "short" || col[1] == "ushort" ||
                         col[1] == "int16" || col[1] == "uint16")
                {
                    switch (prop)
                    {
                        case DataProperty.Invalid: prop = DataProperty.Data16; break;
                        case DataProperty.R8: prop = DataProperty.R16; break;
                        case DataProperty.G8: prop = DataProperty.G16; break;
                        case DataProperty.B8: prop = DataProperty.B16; break;
                        case DataProperty.A8: prop = DataProperty.A16; break;
                    }
                    if (GetPropertySize(prop) != 2)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else if (col[1] == "int" || col[1] == "uint" || col[1] == "float" ||
                         col[1] == "int32" || col[1] == "uint32" || col[1] == "float32")
                {
                    if (prop == DataProperty.Invalid)
                        prop = DataProperty.Data32;
                    else if (GetPropertySize(prop) != 4)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else if (col[1] == "int64" || col[1] == "uint64" ||
                         col[1] == "double" || col[1] == "float64")
                {
                    switch (prop)
                    {
                        case DataProperty.Invalid: prop = DataProperty.Data64; break;
                        case DataProperty.SingleX: prop = DataProperty.DoubleX; break;
                        case DataProperty.SingleY: prop = DataProperty.DoubleY; break;
                        case DataProperty.SingleZ: prop = DataProperty.DoubleZ; break;
                        case DataProperty.SingleNX: prop = DataProperty.DoubleNX; break;
                        case DataProperty.SingleNY: prop = DataProperty.DoubleNY; break;
                        case DataProperty.SingleNZ: prop = DataProperty.DoubleNZ; break;
                    }
                    if (GetPropertySize(prop) != 8)
                        throw new ArgumentException("Invalid property type ('" + line + "').");
                }
                else
                {
                    throw new ArgumentException("Unsupported property type ('" + line + "').");
                }

                data.properties.Add(prop);
            }
        }

        // Rewind the stream back to the exact position of the reader.
        reader.BaseStream.Position = readCount;

        return data;
    }

    DataBody ReadDataBody(DataHeader header, BinaryReader reader)
    {
        var data = new DataBody(header.vertexCount);

        float x = 0, y = 0, z = 0;
        float nx = 0, ny = 0, nz = 0;
        Byte r = 255, g = 255, b = 255, a = 255;

        for (var i = 0; i < header.vertexCount; i++)
        {
            foreach (var prop in header.properties)
            {
                switch (prop)
                {
                    case DataProperty.R8: r = reader.ReadByte(); break;
                    case DataProperty.G8: g = reader.ReadByte(); break;
                    case DataProperty.B8: b = reader.ReadByte(); break;
                    case DataProperty.A8: a = reader.ReadByte(); break;

                    case DataProperty.R16: r = (byte)(reader.ReadUInt16() >> 8); break;
                    case DataProperty.G16: g = (byte)(reader.ReadUInt16() >> 8); break;
                    case DataProperty.B16: b = (byte)(reader.ReadUInt16() >> 8); break;
                    case DataProperty.A16: a = (byte)(reader.ReadUInt16() >> 8); break;

                    case DataProperty.SingleX: x = reader.ReadSingle(); break;
                    case DataProperty.SingleY: y = reader.ReadSingle(); break;
                    case DataProperty.SingleZ: z = reader.ReadSingle(); break;

                    case DataProperty.DoubleX: x = (float)reader.ReadDouble(); break;
                    case DataProperty.DoubleY: y = (float)reader.ReadDouble(); break;
                    case DataProperty.DoubleZ: z = (float)reader.ReadDouble(); break;


                    case DataProperty.SingleNX: nx = reader.ReadSingle(); break;
                    case DataProperty.SingleNY: ny = reader.ReadSingle(); break;
                    case DataProperty.SingleNZ: nz = reader.ReadSingle(); break;

                    case DataProperty.DoubleNX: nx = (float)reader.ReadDouble(); break;
                    case DataProperty.DoubleNY: ny = (float)reader.ReadDouble(); break;
                    case DataProperty.DoubleNZ: nz = (float)reader.ReadDouble(); break;

                    case DataProperty.Data8: reader.ReadByte(); break;
                    case DataProperty.Data16: reader.BaseStream.Position += 2; break;
                    case DataProperty.Data32: reader.BaseStream.Position += 4; break;
                    case DataProperty.Data64: reader.BaseStream.Position += 8; break;
                }
            }

            data.AddPoint(x, y, z, r, g, b, a, nx, ny, nz);
        }

        return data;
    }

    enum DataProperty
    {
        Invalid,
        R8, G8, B8, A8,
        R16, G16, B16, A16,
        SingleX, SingleY, SingleZ,
        DoubleX, DoubleY, DoubleZ,
        Data8, Data16, Data32, Data64,
        SingleNX, SingleNY, SingleNZ,
        DoubleNX, DoubleNY, DoubleNZ

    }

    static int GetPropertySize(DataProperty p)
    {
        switch (p)
        {
            case DataProperty.R8: return 1;
            case DataProperty.G8: return 1;
            case DataProperty.B8: return 1;
            case DataProperty.A8: return 1;
            case DataProperty.R16: return 2;
            case DataProperty.G16: return 2;
            case DataProperty.B16: return 2;
            case DataProperty.A16: return 2;
            case DataProperty.SingleX: return 4;
            case DataProperty.SingleY: return 4;
            case DataProperty.SingleZ: return 4;
            case DataProperty.DoubleX: return 8;
            case DataProperty.DoubleY: return 8;
            case DataProperty.DoubleZ: return 8;
            case DataProperty.SingleNX: return 4;
            case DataProperty.SingleNY: return 4;
            case DataProperty.SingleNZ: return 4;
            case DataProperty.DoubleNX: return 8;
            case DataProperty.DoubleNY: return 8;
            case DataProperty.DoubleNZ: return 8;
            case DataProperty.Data8: return 1;
            case DataProperty.Data16: return 2;
            case DataProperty.Data32: return 4;
            case DataProperty.Data64: return 8;
        }
        return 0;
    }

    class DataHeader
    {
        public List<DataProperty> properties = new List<DataProperty>();
        public int vertexCount = -1;
    }

    class DataBody
    {
        public List<Vector3> vertices;
        public List<Color32> colors;
        public List<Vector3> normals;

        public DataBody(int vertexCount)
        {
            vertices = new List<Vector3>(vertexCount);
            colors = new List<Color32>(vertexCount);
            normals = new List<Vector3>(vertexCount);
        }

        public void AddPoint(
            float x, float y, float z,
            byte r, byte g, byte b, byte a
        )
        {
            vertices.Add(new Vector3(x, z, y));
            colors.Add(new Color32(r, g, b, a));
        }
        public void AddPoint(
            float x, float y, float z,
            byte r, byte g, byte b, byte a, float nx, float ny, float nz
        )
        {
            vertices.Add(new Vector3(x, z, y));
            colors.Add(new Color32(r, g, b, a));
            normals.Add(new Vector3(nx, nz, ny));
        }
    }

    #endregion

}
