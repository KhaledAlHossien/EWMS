using System.Text;
using Application.Features.Attendance.Push;

namespace Attendance.Tests
{
    public class PushProtocolTests
    {
        [Fact]
        public void ParseAttLog_reads_fields_and_skips_broken_lines()
        {
            var body = "1001\t2026-10-26 08:01:33\t0\t15\t0\t0\t0\r\n"
                     + "1002\t2026-10-26 08:02:00\t1\t1\n"
                     + "broken line\n"
                     + "1003\t26/10/2026 08:00\t0\t1\n"
                     + "\n";

            var punches = PushProtocol.ParseAttLog(body);

            Assert.Equal(2, punches.Count);
            Assert.Equal(new PushPunch("1001", new DateTime(2026, 10, 26, 8, 1, 33), 0, 15, "0"), punches[0]);
            Assert.Equal("1002", punches[1].Pin);
            Assert.Equal(1, punches[1].Status);
            Assert.Equal(1, punches[1].Verify);
            Assert.Equal(string.Empty, punches[1].WorkCode);
        }

        [Fact]
        public void ParseOperations_reads_users_templates_and_photos()
        {
            var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
            var body = "USER PIN=1001\tName=أحمد\tPri=14\tPasswd=\tCard=123456\tGrp=1\tTZ=0000000100000000\n"
                     + "BIODATA Pin=1001\tNo=0\tIndex=0\tValid=1\tDuress=0\tType=9\tMajorVer=58\tMinorVer=12\tFormat=0\tTmp=QUJD==\n"
                     + "FP PIN=1002\tFID=6\tSize=4\tValid=1\tTMP=REVG\n"
                     + $"BIOPHOTO PIN=1001\tFileName=1001.jpg\tType=9\tSize={jpeg.Length}\tContent={Convert.ToBase64String(jpeg)}\n"
                     + "OPLOG 4\t0\t2026-10-26 08:00:00\t0\t0\t0\t0\n";

            var ops = PushProtocol.ParseOperations(body);

            var user = Assert.Single(ops.Users);
            Assert.Equal(new PushUser("1001", "أحمد", 14, "123456"), user);

            Assert.Equal(2, ops.Templates.Count);
            var face = ops.Templates[0];
            Assert.Equal(("1001", 9, "58", "12", "QUJD=="), (face.Pin, face.Type, face.MajorVersion, face.MinorVersion, face.Template));
            var finger = ops.Templates[1];
            Assert.Equal(("1002", 1, 6, "REVG"), (finger.Pin, finger.Type, finger.No, finger.Template));

            var photo = Assert.Single(ops.BioPhotos);
            Assert.Equal(jpeg, photo.Content);
        }

        [Fact]
        public void ParseInfo_reads_firmware_counts_and_algorithms()
        {
            var info = PushProtocol.ParseInfo("Ver 6.60 Apr 28 2020,25,40,1234,192.168.1.201,10,58,12,20,111");

            Assert.Equal("6.60 Apr 28 2020", info.Firmware);
            Assert.Equal(25, info.Users);
            Assert.Equal(40, info.Fingers);
            Assert.Equal(1234, info.Punches);
            Assert.Equal("192.168.1.201", info.Ip);
            Assert.Equal("10", info.FingerAlgorithm);
            Assert.Equal("58", info.FaceAlgorithm);
            Assert.Equal(20, info.Faces);
            Assert.Same(PushDeviceInfo.Empty, PushProtocol.ParseInfo(null));
        }

        [Fact]
        public void ParseOptions_reads_device_name_and_versions()
        {
            var info = PushProtocol.ParseOptions("~DeviceName=SpeedFace-V5L,MAC=00:17:61:00:00:01,FirmVer=ZAM230-NF-1.0,UserCount=3,FaceVersion=58,FPVersion=10,IPAddress=10.0.0.5");

            Assert.Equal("SpeedFace-V5L", info.Model);
            Assert.Equal("ZAM230-NF-1.0", info.Firmware);
            Assert.Equal(3, info.Users);
            Assert.Equal("58", info.FaceAlgorithm);
            Assert.Equal("10", info.FingerAlgorithm);
            Assert.Equal("10.0.0.5", info.Ip);
        }

        [Fact]
        public void ParseCommandResults_reads_each_line()
        {
            var results = PushProtocol.ParseCommandResults("ID=12&Return=0&CMD=DATA\nID=13&Return=-1002&CMD=DATA\nnonsense\n");

            Assert.Equal([new PushCommandResult(12, 0, "DATA"), new PushCommandResult(13, -1002, "DATA")], results);
        }

        [Fact]
        public void ParsePunchPhoto_reads_time_and_pin_from_the_file_name()
        {
            var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xDB, 9, 9 };
            var body = Encoding.ASCII.GetBytes("PIN=20261026080133-1001.jpg\nSN=AJE1261000778\nsize=6\nCMD=uploadphoto\0").Concat(jpeg).ToArray();

            var photo = PushProtocol.ParsePunchPhoto(body);

            Assert.NotNull(photo);
            Assert.Equal("1001", photo!.Pin);
            Assert.Equal(new DateTime(2026, 10, 26, 8, 1, 33), photo.Time);
            Assert.Equal(jpeg, photo.Content);
            // صورة شخص غير معروف (بلا رقم) أو محتوى ليس صورة: تُهمل
            Assert.Null(PushProtocol.ParsePunchPhoto(Encoding.ASCII.GetBytes("PIN=20261026080133.jpg\0").Concat(jpeg).ToArray()));
            Assert.Null(PushProtocol.ParsePunchPhoto(Encoding.ASCII.GetBytes("PIN=20261026080133-1.jpg\0abc")));
        }

        [Fact]
        public void Handshake_returns_stamps_and_settings()
        {
            var text = PushProtocol.Handshake("AJE1261000778", "111", "222", "333", 10, 3, wantPhotos: true);

            Assert.StartsWith("GET OPTION FROM: AJE1261000778\n", text);
            Assert.Contains("ATTLOGStamp=111\n", text);
            Assert.Contains("OPERLOGStamp=222\n", text);
            Assert.Contains("ATTPHOTOStamp=333\n", text);
            Assert.Contains("Delay=10\n", text);
            Assert.Contains("TimeZone=3\n", text);
            Assert.Contains("Realtime=1\n", text);
            Assert.Contains("AttPhoto", text);
        }

        [Fact]
        public void FormatCommands_writes_one_line_per_command_or_OK()
        {
            Assert.Equal("OK", PushProtocol.FormatCommands([]));
            Assert.Equal("C:5:INFO\nC:6:REBOOT\n", PushProtocol.FormatCommands([(5, "INFO"), (6, "REBOOT")]));
        }

        [Fact]
        public void UpdateUser_cleans_the_name_and_sets_privilege()
        {
            var text = PushProtocol.UpdateUser("1001", "محمد\tأحمد=الأحمد\nاسم طويل جداً يتجاوز الحد", deviceAdmin: true, card: "55");

            Assert.StartsWith("DATA UPDATE USERINFO PIN=1001\tName=", text);
            var name = text.Split('\t')[1]["Name=".Length..];
            Assert.DoesNotContain('=', name);
            Assert.True(name.Length <= PushProtocol.MaxNameLength);
            Assert.Contains("\tPri=14\t", text);
            Assert.Contains("\tCard=55\t", text);
        }

        [Fact]
        public void Commands_have_the_expected_shape()
        {
            Assert.Equal("DATA DELETE USERINFO PIN=7", PushProtocol.DeleteUser("7"));
            Assert.Equal("DATA QUERY ATTLOG StartTime=2026-10-01 00:00:00\tEndTime=2026-10-26 23:59:59",
                PushProtocol.QueryPunches(new DateTime(2026, 10, 1), new DateTime(2026, 10, 26, 23, 59, 59)));
            Assert.Equal("DATA UPDATE BIODATA Pin=7\tNo=0\tIndex=0\tValid=1\tDuress=0\tType=9\tMajorVer=58\tMinorVer=12\tFormat=0\tTmp=QUJD",
                PushProtocol.UpdateTemplate("7", 9, 0, 0, true, false, "58", "12", 0, "QUJD"));
        }

        [Fact]
        public void EncodeTime_counts_seconds_from_2000_with_31_day_months()
        {
            Assert.Equal(0, PushProtocol.EncodeTime(new DateTime(2000, 1, 1)));
            Assert.Equal(86401, PushProtocol.EncodeTime(new DateTime(2000, 1, 2, 0, 0, 1)));
            Assert.Equal(31L * 86400, PushProtocol.EncodeTime(new DateTime(2000, 2, 1)));
            Assert.Equal(12L * 31 * 86400, PushProtocol.EncodeTime(new DateTime(2001, 1, 1)));
        }
    }
}
