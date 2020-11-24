using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System;
using System.IO;
using UnityEngine.Networking;

public class particle : MonoBehaviour
{
    public Text debug;
    string taskText = "";
    float deltaTime;
    public float fps;
    ParticleSystem m_System;
    public float pointSize = 0.05f;

    string filePath;

    int selectedFile;
    public int nPoints;
    public bool getOnline = true;

    PlyPointCloudData plyDown = new PlyPointCloudData(rendererType.CPU_Shuriken);
    public bool hasUpdated = false;

    public string[] files = new string[] {
        @"C:\Users\Gladys\Documents\Repos\DDL_BirdSim\Assets\Resources\Data\treeData\Tree5\Ground - 15-02 s1 tu-kh1 xm1 Ground 1cm.ply",
        @"C:\Users\Gladys\Documents\Repos\DDL_BirdSim\Assets\Resources\Data\treeData\Tree5\Leaves - 15 - 02 s1 tu - kh1 xm1 Leaves 1cm.ply",
        @"C:\Users\Gladys\Documents\Repos\DDL_BirdSim\Assets\Resources\Data\treeData\Tree5\Tree - 15-02 s1 tu-kh1 xm1 Tree 1cm.ply" };


    // Start is called before the first frame update
    void Start()
    {

        InitializeIfNeeded();

        if (getOnline)
        {
            StartCoroutine(plyDown.GetRequest("http://www.julianrutten.com/Data/Tree.ply"));
        }
        else
        {
            if (selectedFile < files.Length)
            {
                //nPoints = ImportPointCloud(files[selectedFile]);
            }
            else
            {
                //nPoints = ImportPointCloud(filePath);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
        fps = 1.0f / deltaTime;
        debug.text = taskText + Mathf.Ceil(fps).ToString();

        if (plyDown.isFinished && !hasUpdated)
        {
            hasUpdated = true;

            Debug.Log("redrawing...");

            PointCloud data = new PointCloud();
            data.name = plyDown.name;
            data.pointSize = pointSize;
            nPoints = plyDown.getPositions.Count;
            data.Initialize(plyDown.getPositions, plyDown.getColours, plyDown.getNormals);
            data.setParticles(m_System);
        }
    }

    void InitializeIfNeeded()
    {
        if (m_System == null)
            m_System = GetComponent<ParticleSystem>();

        // if (m_Particles == null || m_Particles.Length < m_System.main.maxParticles)
        //  m_Particles = new ParticleSystem.Particle[m_System.main.maxParticles];
    }
    
    /*
    IEnumerator GetRequest(string uri)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {

            taskText = "\nDownloading Pointcloud...\n";
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

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
                    nPoints = ImportPointCloud(webRequest.downloadHandler.data, fileName[0]);
                }
            }

            taskText = "\nLoaded Pointcloud...\n";
        }
    }

    int ImportPointCloud(byte[] streamedData, string fileName)
    {
        try
        {
            MemoryStream stream = new MemoryStream(streamedData);
            var header = ReadDataHeader(new StreamReader(stream));
            var body = ReadDataBody(header, new BinaryReader(stream));

            // var data = ScriptableObject.CreateInstance<BakedPointCloud>();
            PointCloud data = new PointCloud();
            data.name = fileName;
            data.pointSize = pointSize;
            data.Initialize(body.vertices, body.colors, body.normals);
            data.setParticles(m_System);
            return body.vertices.Count;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed importing " + fileName + ". \n" + e.Message);
        }
        return -1;
    }


    int ImportPointCloud(string path)
    {
        try
        {
            var stream = File.Open(@path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = ReadDataHeader(new StreamReader(stream));
            var body = ReadDataBody(header, new BinaryReader(stream));

            // var data = ScriptableObject.CreateInstance<BakedPointCloud>();
            PointCloud data = new PointCloud();
            data.name = Path.GetFileNameWithoutExtension(@path);
            data.pointSize = pointSize;
            data.Initialize(body.vertices, body.colors, body.normals);
            data.setParticles(m_System);
            return body.vertices.Count;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed importing " + path + ". \n" + e.Message);
        }
        return -1;
    }

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

    int GetPropertySize(DataProperty p)
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





    #region Internal data structure

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
            normals.Add(new Vector3(nx, ny, nz));
        }
    }

    #endregion
    */
}
