namespace ZZZScanner.Helpers;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Text;

public class Recognizer : IDisposable
{
    // 字符字典，模型决定，共18383行
    private readonly string[] _characterDict;
    private readonly InferenceSession _session;

    public Recognizer(string modelFile, string characterDictFile)
    {
        _characterDict = File.ReadAllLines(characterDictFile);
        _session = new InferenceSession(modelFile);
    }

    public List<OcrResult> Recognize(Mat src, Rect[] rois)
    {
        var inputs = CreateInputs(src, rois);
        using var outputs = _session.Run(inputs);
        var result = CTCLabelDecode(outputs);
        return result;
    }

    public void Dispose()
    {
        _session.Dispose();
    }

    public readonly struct OcrResult
    {
        public float Score { get; }
        public string Text { get; }

        public OcrResult(float score, string text)
        {
            Score = score;
            Text = text;
        }

        public void Deconstruct(out float score, out string text)
        {
            score = Score;
            text = Text;
        }

        public override string ToString()
        {
            return $"{Score * 100,6:F2} {Text}";
        }
    }

    // 输入名称，模型决定，该模型只有1个输入和1个输出
    private readonly string _inputName = "x";
    // 图片通道，模型决定
    private readonly int _channel = 3;
    // 最大宽高，模型决定
    private readonly int _maxHeight = 48;
    // 归一参数，模型决定
    private readonly double _alpha = 1.0 / 255.0;

    private int GetResizedWidth(int width, int height)
    {
        return (int)(width * (float)_maxHeight / height + 0.5f);
    }

    private IReadOnlyCollection<NamedOnnxValue> CreateInputs(Mat src, Rect[] rois)
    {
        var maxWidth = rois.Max(x => GetResizedWidth(x.Width, x.Height));

        // 只有一个输入
        var tensor = new DenseTensor<float>(new int[] { rois.Length, _channel, _maxHeight, maxWidth });
        // 预计算索引
        var dim = tensor.Dimensions;
        var batchStride = dim[1] * dim[2] * dim[3];
        var channelStride = dim[2] * dim[3];
        var heightStride = dim[3];

        for (int b = 0; b < rois.Length; b++)
        {
            var roi = rois[b];
            var newWidth = GetResizedWidth(roi.Width, roi.Height);
            var newSize = new OpenCvSharp.Size(newWidth, _maxHeight);
            var newRoi = new Rect(0, 0, newWidth, _maxHeight);

            using var roiMat = new Mat(src, roi);
            using var resizedMat = new Mat();
            using var dest = new Mat(_maxHeight, maxWidth, MatType.CV_32FC4);
            using var destRoi = new Mat(dest, newRoi);

            dest.SetTo(0);
            // 按高度调整宽度
            Cv2.Resize(roiMat, resizedMat, newSize);
            // 转浮点并归一
            resizedMat.ConvertTo(destRoi, dest.Type(), _alpha);
            unsafe
            {
                dest.ForEachAsVec4f((value, position) =>
                {
                    var h = position[0];
                    var w = position[1];
                    var index = b * batchStride + 0 * channelStride + h * heightStride + w;
                    // 复制到tensor，直接访问更快
                    var span = tensor.Buffer.Span;
                    span[index + 0 * channelStride] = value->Item0; // B
                    span[index + 1 * channelStride] = value->Item1; // G
                    span[index + 2 * channelStride] = value->Item2; // R
                });
            }
        }

        return new[] { NamedOnnxValue.CreateFromTensor(_inputName, tensor) };
    }

    private List<OcrResult> CTCLabelDecode(IEnumerable<NamedOnnxValue> outputs)
    {
        var list = new List<OcrResult>();

        // 只有一个输出
        var tensor = (DenseTensor<float>)outputs.First().Value;
        // 预计算索引
        var dim = tensor.Dimensions;
        var batchStride = dim[1] * dim[2];
        var typeStride = dim[2];
        // 直接访问更快速
        var span = tensor.Buffer.Span;

        // 第b个图片/区域
        for (int b = 0; b < dim[0]; b++)
        {
            var batchOffset = b * batchStride;

            var sb = new StringBuilder();
            var count = 0;
            var score = 0f;

            // 第t个字符
            for (int t = 0; t < dim[1]; t++)
            {
                var typeOffset = batchOffset + t * typeStride;

                var maxIndex = -1;
                var maxScore = float.MinValue;

                // 是哪个字符
                for (int i = 0; i < dim[2]; i++)
                {
                    var currValue = span[typeOffset + i];
                    if (!float.IsNaN(currValue) && currValue > maxScore)
                    {
                        maxScore = currValue;
                        maxIndex = i;
                    }
                }

                // 索引有1的偏移
                maxIndex--;
                if (0 <= maxIndex && maxIndex < _characterDict.Length)
                {
                    count++;
                    score += maxScore;
                    sb.Append(_characterDict[maxIndex]);
                }
                else if (maxIndex == _characterDict.Length)
                {
                    break;
                }
            }

            if (count > 0)
            {
                score /= count;
                list.Add(new(score, sb.ToString()));
            }
        }

        return list;
    }
}
