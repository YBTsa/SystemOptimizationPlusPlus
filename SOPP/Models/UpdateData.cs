using System.IO;
using System.Xml.Serialization;

namespace SOPP.Models
{
    public class UpdateData
    {
        // 是否首次加载（是否检查过更新）
        public bool IsFirstLoad { get; set; } = true;

        // 保存数据到文件
        public void Save(string filePath = "update_data.xml")
        {
            var serializer = new XmlSerializer(typeof(UpdateData));
            using var writer = new StreamWriter(filePath);
            serializer.Serialize(writer, this);
        }

        // 从文件加载数据
        public static UpdateData Load(string filePath = "update_data.xml")
        {
            if (!File.Exists(filePath))
            {
                return new UpdateData();
            }

            var serializer = new XmlSerializer(typeof(UpdateData));
            using var reader = new StreamReader(filePath);
            return (UpdateData)serializer.Deserialize(reader);
        }
    }
}
