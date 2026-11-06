using SignageVienBa.Core;

namespace SignageVienBa.Tests;

public class TvVideoTests
{
    private static VideoInfo Info(double seconds = 60, int w = 1920, int h = 1080, string codec = "h264",
        string pix = "yuv420p", double fps = 30, int rotation = 0, string? audio = "aac", long size = 50 << 20) =>
        new(seconds, w, h, codec, pix, fps, rotation, audio, size);

    [Fact]
    public void Short_videos_are_capped_at_8_mbps() => Assert.Equal(8000, TvVideo.VideoKbps(Info(seconds: 20)));

    [Fact]
    public void Long_videos_get_the_bitrate_that_fits_85_mb()
    {
        // 85 MB = 696 320 kbit over 600 s = 1160 kbps, minus 128 for audio.
        Assert.Equal(1032, TvVideo.VideoKbps(Info(seconds: 600)));
        Assert.Equal(1160, TvVideo.VideoKbps(Info(seconds: 600, audio: null)));
    }

    [Fact]
    public void Videos_too_long_to_fit_are_refused_with_a_hint()
    {
        var e = Assert.Throws<SignageException>(() => TvVideo.VideoKbps(Info(seconds: 3 * 3600)));
        Assert.Contains("cắt ngắn", e.Message);
        Assert.Contains("35 phút", e.Message);
    }

    [Fact]
    public void Ready_means_1080x1920_h264_30fps_mp4_under_90_mb()
    {
        var ready = Info(w: 1080, h: 1920);
        Assert.True(TvVideo.IsReady(ready, "a.mp4"));
        Assert.True(TvVideo.IsReady(ready with { Fps = 29.97, AudioCodec = null }, "a.MP4"));
        Assert.False(TvVideo.IsReady(ready, "a.mov"));
        Assert.False(TvVideo.IsReady(ready with { Width = 1920, Height = 1080 }, "a.mp4"));
        Assert.False(TvVideo.IsReady(ready with { VideoCodec = "hevc" }, "a.mp4"));
        Assert.False(TvVideo.IsReady(ready with { Fps = 60 }, "a.mp4"));
        Assert.False(TvVideo.IsReady(ready with { Rotation = 90 }, "a.mp4"));
        Assert.False(TvVideo.IsReady(ready with { PixelFormat = "yuv420p10le" }, "a.mp4"));
        Assert.False(TvVideo.IsReady(ready with { AudioCodec = "pcm_s16le" }, "a.mp4"));
        Assert.False(TvVideo.IsReady(ready with { SizeBytes = 91L << 20 }, "a.mp4"));
    }

    [Fact]
    public void Filter_fits_the_picture_inside_the_frame_and_limits_fps_only_when_needed()
    {
        var graph = TvVideo.FilterGraph(Info(fps: 60));
        Assert.StartsWith("[0:v]fps=30,split=2", graph);
        Assert.Contains("scale=1080:1920:force_original_aspect_ratio=decrease", graph);
        Assert.DoesNotContain("fps=", TvVideo.FilterGraph(Info(fps: 25)));
    }

    [Fact]
    public void Encode_args_map_audio_only_when_present()
    {
        var withAudio = TvVideo.EncodeArgs("in.mov", "out.mp4", Info());
        Assert.Contains("0:a:0", withAudio);
        Assert.Contains("aac", withAudio);
        Assert.Equal("out.mp4", withAudio[^1]);

        var silent = TvVideo.EncodeArgs("in.mov", "out.mp4", Info(audio: null));
        Assert.DoesNotContain("0:a:0", silent);
        Assert.Contains("-an", silent);
    }

    [Fact]
    public void Probe_json_is_parsed_skipping_cover_art_and_reading_rotation()
    {
        const string json = """
            {
              "streams": [
                { "codec_type": "video", "codec_name": "mjpeg", "width": 300, "height": 300,
                  "disposition": { "attached_pic": 1 } },
                { "codec_type": "video", "codec_name": "hevc", "width": 1920, "height": 1080,
                  "pix_fmt": "yuv420p10le", "avg_frame_rate": "30000/1001", "r_frame_rate": "30/1",
                  "disposition": { "attached_pic": 0 },
                  "side_data_list": [ { "side_data_type": "Display Matrix", "rotation": -90 } ] },
                { "codec_type": "audio", "codec_name": "aac" }
              ],
              "format": { "duration": "12.500000", "size": "1234" }
            }
            """;
        var info = VideoTools.ParseProbe(json, 1234);
        Assert.Equal("hevc", info.VideoCodec);
        Assert.Equal(1920, info.Width);
        Assert.Equal(-90, info.Rotation);
        Assert.Equal(29.97, info.Fps, 2);
        Assert.Equal(12.5, info.DurationSeconds);
        Assert.Equal("aac", info.AudioCodec);
    }

    [Fact]
    public void Probe_without_a_picture_stream_is_refused()
    {
        const string json = """{ "streams": [ { "codec_type": "audio", "codec_name": "mp3" } ], "format": { "duration": "3" } }""";
        var e = Assert.Throws<SignageException>(() => VideoTools.ParseProbe(json, 10));
        Assert.Contains("không có hình", e.Message);
    }
}
