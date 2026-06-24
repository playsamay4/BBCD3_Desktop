using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBCD3_Desktop
{

    public class Source
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string UrlPrefix { get; set; }

        public string VideoPath { get; set; } = "v=pv14/b=5070016";

        public string AudioPath { get; set; } = "a=pa3/al=en-GB/ap=main/b=96000";

        public string GetUrlPrefixForProvider(string providerBaseUrl)
        {
            if (string.IsNullOrEmpty(UrlPrefix)) return UrlPrefix;

            // Find where the trailing endpoint parameters begin (look for "x=4/" or fallback to finding the 3rd slash)
            int markerIdx = UrlPrefix.IndexOf("x=4/");
            if (markerIdx == -1)
            {
                // Fallback heuristic: Skip the "https://" protocol and find the next slash
                int firstSlash = UrlPrefix.IndexOf("//");
                int searchStart = firstSlash != -1 ? firstSlash + 2 : 0;
                markerIdx = UrlPrefix.IndexOf('/', searchStart);
                if (markerIdx != -1) markerIdx++; // Move past the slash
            }

            if (markerIdx == -1) return UrlPrefix; // Return unchanged if structural parsing fails

            string streamPath = UrlPrefix.Substring(markerIdx);

            // Ensure proper trailing slash combinations
            string baseUrl = providerBaseUrl.EndsWith("/") ? providerBaseUrl : providerBaseUrl + "/";
            return baseUrl + streamPath;
        }
    }



    public static class SOURCES
    {
        public static readonly string[] PROVIDERS = new string[]
        {
            "https://vs-cmaf-push-uk-live.akamaized.net/",
            "https://vs-cmaf-pushb-uk-live.akamaized.net/",
            "https://vs-cmaf-push-uk.live.fastly.md.bbci.co.uk/",
            "https://vs-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/",
            "https://vs-cmaf-push-uk.live.cf.md.bbci.co.uk/",
            "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/",
            "https://abntg5gaaaaaaaamcpnzseumjzfnu.vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/",
            "https://abntg5gaaaaaaaamcpnzseumjzfnu.vs-cmaf-push-uk.live.cf.md.bbci.co.uk/"
        };


        public static Dictionary<string, Source> All = new Dictionary<string, Source>
        {
            // BBC News
            { "news_uk", new Source { Id = "news_uk", Name = "BBC News UK", Category = "News", UrlPrefix = "https://vs-cmaf-push-ww-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_news_channel_hd/" } },
            { "news_uk_fhd", new Source {
                Id = "news_uk_fhd",
                Name = "BBC News UK",
                Category = "News FHD",
                UrlPrefix = "https://vs-cmaf-push-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_news_channel_hd/",
                VideoPath = "v=pv66/b=6500000",
                AudioPath = "a=pa6/al=en-GB/ap=main/b=320000"
            } },
            { "news_na", new Source { Id = "news_na", Name = "BBC News World (North America) [US-Only Geoblock]", Category = "News", UrlPrefix = "https://vs-cmaf-pushb-ntham-gcomm-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_world_news_north_america/" } },
            { "news_apac", new Source { Id = "news_apac", Name = "BBC News World (Asia-Pacific) [Australia-Only Geoblock]", Category = "News", UrlPrefix = "https://vs-cmaf-pushb-apac-gcomm.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_world_news_asia_pacific/" } },
            { "news_ar", new Source { Id = "news_ar", Name = "BBC News Arabic", Category = "News", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_arabic_tv/" } },
            { "news_fa", new Source { Id = "news_fa", Name = "BBC News Persian", Category = "News", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_persian_tv/" } },

            //BBC One HD
            { "one_lon", new Source { Id = "one_lon", Name = "BBC One London [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-push-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_london/" } },
            { "one_wal", new Source { Id = "one_wal", Name = "BBC One Wales [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_wales_hd/" } },
            { "one_sco", new Source { Id = "one_sco", Name = "BBC One Scotland [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_scotland_hd/" } },
            { "one_ni",  new Source { Id = "one_ni",  Name = "BBC One Northern Ireland [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_northern_ireland_hd/" } },
            { "one_ci",  new Source { Id = "one_ci",  Name = "BBC One Channel Islands [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_channel_islands/" } },
            { "one_east", new Source { Id = "one_east", Name = "BBC One East [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east/" } },
            { "one_em", new Source { Id = "one_em", Name = "BBC One East Midlands [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east_midlands/" } },
            { "one_ey", new Source { Id = "one_ey", Name = "BBC One East Yorks & Lincs [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east_yorkshire/" } },
            { "one_ne", new Source { Id = "one_ne", Name = "BBC One North East [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_one_north_east/" } },
            { "one_nw", new Source { Id = "one_nw", Name = "BBC One North West [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_north_west/" } },
            { "one_sou", new Source { Id = "one_sou", Name = "BBC One South [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south/" } },
            { "one_se", new Source { Id = "one_se", Name = "BBC One South East [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south_east/" } },
            { "one_sw", new Source { Id = "one_sw", Name = "BBC One South West [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south_west/" } },
            { "one_wes", new Source { Id = "one_wes", Name = "BBC One West [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west/" } },
            { "one_wm", new Source { Id = "one_wm", Name = "BBC One West Midlands [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west_midlands/" } },
            { "one_yor", new Source { Id = "one_yor", Name = "BBC One Yorkshire [UK Only]", Category = "BBC One", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_yorks/" } },

            // BBC ONE FHD
            { "one_lon_fhd", new Source { Id = "one_lon_fhd", Name = "BBC One London [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-push-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_london/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_wal_fhd", new Source { Id = "one_wal_fhd", Name = "BBC One Wales [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_wales_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_sco_fhd", new Source { Id = "one_sco_fhd", Name = "BBC One Scotland [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_scotland_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_ni_fhd",  new Source { Id = "one_ni_fhd",  Name = "BBC One Northern Ireland [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_northern_ireland_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_ci_fhd",  new Source { Id = "one_ci_fhd",  Name = "BBC One Channel Islands [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_channel_islands/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_east_fhd", new Source { Id = "one_east_fhd", Name = "BBC One East [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_em_fhd", new Source { Id = "one_em_fhd", Name = "BBC One East Midlands [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east_midlands/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_ey_fhd", new Source { Id = "one_ey_fhd", Name = "BBC One East Yorks & Lincs [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_east_yorkshire/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_ne_fhd", new Source { Id = "one_ne_fhd", Name = "BBC One North East [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_north_east/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_nw_fhd", new Source { Id = "one_nw_fhd", Name = "BBC One North West [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_north_west/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_sou_fhd", new Source { Id = "one_sou_fhd", Name = "BBC One South [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_se_fhd", new Source { Id = "one_se_fhd", Name = "BBC One South East [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south_east/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_sw_fhd", new Source { Id = "one_sw_fhd", Name = "BBC One South West [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_south_west/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_wes_fhd", new Source { Id = "one_wes_fhd", Name = "BBC One West [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_wm_fhd", new Source { Id = "one_wm_fhd", Name = "BBC One West Midlands [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_west_midlands/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "one_yor_fhd", new Source { Id = "one_yor_fhd", Name = "BBC One Yorkshire [UK Only]", Category = "BBC One FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_one_yorks/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },

            // Other bbc HD
            { "two_eng", new Source { Id = "two_eng", Name = "BBC Two England [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-push-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_hd/" } },
            { "two_ni", new Source { Id = "two_ni", Name = "BBC Two Northern Ireland [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_northern_ireland_hd/" } },
            { "two_wal", new Source { Id = "two_wal", Name = "BBC Two Wales [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_wales_digital/" } },
            { "three", new Source { Id = "three", Name = "BBC THREE [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_three_hd/" } },
            { "four", new Source { Id = "four", Name = "BBC Four [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_four_hd/" } },
            { "cbbc", new Source { Id = "cbbc", Name = "CBBC [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:cbbc_hd/" } },
            { "cbeebies", new Source { Id = "cbeebies", Name = "CBeebies [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:cbeebies_hd/" } },
            { "scotland", new Source { Id = "scotland", Name = "BBC Scotland [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_scotland_hd/" } },
            { "parliament", new Source { Id = "parliament", Name = "BBC Parliament [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk-live.akamaized.net/x=4/i=urn:bbc:pips:service:bbc_parliament/" } },
            { "alba", new Source { Id = "alba", Name = "BBC ALBA [UK Only]", Category = "Other BBC", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_alba/" } },

            //Other bbc fhd
            { "two_eng_fhd", new Source { Id = "two_eng_fhd", Name = "BBC Two England [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-push-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "two_ni_fhd", new Source { Id = "two_ni_fhd", Name = "BBC Two Northern Ireland [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_northern_ireland_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "two_wal_fhd", new Source { Id = "two_wal_fhd", Name = "BBC Two Wales [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_two_wales_digital/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "three_fhd", new Source { Id = "three_fhd", Name = "BBC THREE [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_three_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "four_fhd", new Source { Id = "four_fhd", Name = "BBC Four [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_four_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "cbbc_fhd", new Source { Id = "cbbc_fhd", Name = "CBBC [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:cbbc_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "cbeebies_fhd", new Source { Id = "cbeebies_fhd", Name = "CBEEBIES [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:cbeebies_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "scotland_fhd", new Source { Id = "scotland_fhd", Name = "BBC Scotland [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_scotland_hd/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "parliament_fhd", new Source { Id = "parliament_fhd", Name = "BBC Parliament [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_parliament/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "alba_fhd", new Source { Id = "alba_fhd", Name = "BBC ALBA [UK Only]", Category = "Other BBC FHD", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_alba/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },

            { "s4c", new Source { Id = "s4c", Name = "S4C [UK Only]", Category = "S4C", UrlPrefix = "https://vs-cmaf-pushb-uk.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:s4cpbs/" } },

            // streams (UK) 
            { "stream_01_uk", new Source { Id = "stream_01_uk", Name = "BBC STREAM 01 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_001/" } },
            { "stream_01_uk_fhd", new Source { Id = "stream_01_uk_fhd", Name = "BBC STREAM 01 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_001/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_02_uk", new Source { Id = "stream_02_uk", Name = "BBC STREAM 02 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_002/" } },
            { "stream_02_uk_fhd", new Source { Id = "stream_02_uk_fhd", Name = "BBC STREAM 02 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_002/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_03_uk", new Source { Id = "stream_03_uk", Name = "BBC STREAM 03 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_003/" } },
            { "stream_03_uk_fhd", new Source { Id = "stream_03_uk_fhd", Name = "BBC STREAM 03 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_003/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_04_uk", new Source { Id = "stream_04_uk", Name = "BBC STREAM 04 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_004/" } },
            { "stream_04_uk_fhd", new Source { Id = "stream_04_uk_fhd", Name = "BBC STREAM 04 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_004/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_05_uk", new Source { Id = "stream_05_uk", Name = "BBC STREAM 05 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_005/" } },
            { "stream_05_uk_fhd", new Source { Id = "stream_05_uk_fhd", Name = "BBC STREAM 05 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_005/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_06_uk", new Source { Id = "stream_06_uk", Name = "BBC STREAM 06 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_006/" } },
            { "stream_06_uk_fhd", new Source { Id = "stream_06_uk_fhd", Name = "BBC STREAM 06 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_006/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_07_uk", new Source { Id = "stream_07_uk", Name = "BBC STREAM 07 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_007/" } },
            { "stream_07_uk_fhd", new Source { Id = "stream_07_uk_fhd", Name = "BBC STREAM 07 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_007/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_08_uk", new Source { Id = "stream_08_uk", Name = "BBC STREAM 08 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_008/" } },
            { "stream_08_uk_fhd", new Source { Id = "stream_08_uk_fhd", Name = "BBC STREAM 08 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_008/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_09_uk", new Source { Id = "stream_09_uk", Name = "BBC STREAM 09 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_009/" } },
            { "stream_09_uk_fhd", new Source { Id = "stream_09_uk_fhd", Name = "BBC STREAM 09 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_009/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_10_uk", new Source { Id = "stream_10_uk", Name = "BBC STREAM 10 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_010/" } },
            { "stream_10_uk_fhd", new Source { Id = "stream_10_uk_fhd", Name = "BBC STREAM 10 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_010/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_11_uk", new Source { Id = "stream_11_uk", Name = "BBC STREAM 11 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_011/" } },
            { "stream_11_uk_fhd", new Source { Id = "stream_11_uk_fhd", Name = "BBC STREAM 11 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_011/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_12_uk", new Source { Id = "stream_12_uk", Name = "BBC STREAM 12 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_012/" } },
            { "stream_12_uk_fhd", new Source { Id = "stream_12_uk_fhd", Name = "BBC STREAM 12 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_012/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_13_uk", new Source { Id = "stream_13_uk", Name = "BBC STREAM 13 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_013/" } },
            { "stream_13_uk_fhd", new Source { Id = "stream_13_uk_fhd", Name = "BBC STREAM 13 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_013/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_14_uk", new Source { Id = "stream_14_uk", Name = "BBC STREAM 14 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_014/" } },
            { "stream_14_uk_fhd", new Source { Id = "stream_14_uk_fhd", Name = "BBC STREAM 14 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_014/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_15_uk", new Source { Id = "stream_15_uk", Name = "BBC STREAM 15 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_015/" } },
            { "stream_15_uk_fhd", new Source { Id = "stream_15_uk_fhd", Name = "BBC STREAM 15 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_015/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_16_uk", new Source { Id = "stream_16_uk", Name = "BBC STREAM 16 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_016/" } },
            { "stream_16_uk_fhd", new Source { Id = "stream_16_uk_fhd", Name = "BBC STREAM 16 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_016/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_17_uk", new Source { Id = "stream_17_uk", Name = "BBC STREAM 17 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_017/" } },
            { "stream_17_uk_fhd", new Source { Id = "stream_17_uk_fhd", Name = "BBC STREAM 17 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_017/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_18_uk", new Source { Id = "stream_18_uk", Name = "BBC STREAM 18 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_018/" } },
            { "stream_18_uk_fhd", new Source { Id = "stream_18_uk_fhd", Name = "BBC STREAM 18 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_018/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_19_uk", new Source { Id = "stream_19_uk", Name = "BBC STREAM 19 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_019/" } },
            { "stream_19_uk_fhd", new Source { Id = "stream_19_uk_fhd", Name = "BBC STREAM 19 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_019/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_20_uk", new Source { Id = "stream_20_uk", Name = "BBC STREAM 20 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_020/" } },
            { "stream_20_uk_fhd", new Source { Id = "stream_20_uk_fhd", Name = "BBC STREAM 20 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_020/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_21_uk", new Source { Id = "stream_21_uk", Name = "BBC STREAM 21 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_021/" } },
            { "stream_21_uk_fhd", new Source { Id = "stream_21_uk_fhd", Name = "BBC STREAM 21 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_021/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_22_uk", new Source { Id = "stream_22_uk", Name = "BBC STREAM 22 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_022/" } },
            { "stream_22_uk_fhd", new Source { Id = "stream_22_uk_fhd", Name = "BBC STREAM 22 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_022/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_23_uk", new Source { Id = "stream_23_uk", Name = "BBC STREAM 23 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_023/" } },
            { "stream_23_uk_fhd", new Source { Id = "stream_23_uk_fhd", Name = "BBC STREAM 23 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_023/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_51_uk", new Source { Id = "stream_51_uk", Name = "BBC STREAM 51 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_051/" } },
            { "stream_51_uk_fhd", new Source { Id = "stream_51_uk_fhd", Name = "BBC STREAM 51 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_051/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_52_uk", new Source { Id = "stream_52_uk", Name = "BBC STREAM 52 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_052/" } },
            { "stream_52_uk_fhd", new Source { Id = "stream_52_uk_fhd", Name = "BBC STREAM 52 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_052/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_53_uk", new Source { Id = "stream_53_uk", Name = "BBC STREAM 53 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_053/" } },
            { "stream_53_uk_fhd", new Source { Id = "stream_53_uk_fhd", Name = "BBC STREAM 53 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_053/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_54_uk", new Source { Id = "stream_54_uk", Name = "BBC STREAM 54 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_054/" } },
            { "stream_54_uk_fhd", new Source { Id = "stream_54_uk_fhd", Name = "BBC STREAM 54 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_054/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_55_uk", new Source { Id = "stream_55_uk", Name = "BBC STREAM 55 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_055/" } },
            { "stream_55_uk_fhd", new Source { Id = "stream_55_uk_fhd", Name = "BBC STREAM 55 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_055/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_56_uk", new Source { Id = "stream_56_uk", Name = "BBC STREAM 56 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_056/" } },
            { "stream_56_uk_fhd", new Source { Id = "stream_56_uk_fhd", Name = "BBC STREAM 56 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_056/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_57_uk", new Source { Id = "stream_57_uk", Name = "BBC STREAM 57 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_057/" } },
            { "stream_57_uk_fhd", new Source { Id = "stream_57_uk_fhd", Name = "BBC STREAM 57 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_057/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_58_uk", new Source { Id = "stream_58_uk", Name = "BBC STREAM 58 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_058/" } },
            { "stream_58_uk_fhd", new Source { Id = "stream_58_uk_fhd", Name = "BBC STREAM 58 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_058/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_59_uk", new Source { Id = "stream_59_uk", Name = "BBC STREAM 59 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_059/" } },
            { "stream_59_uk_fhd", new Source { Id = "stream_59_uk_fhd", Name = "BBC STREAM 59 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_059/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_64_uk", new Source { Id = "stream_64_uk", Name = "BBC STREAM 64 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_064/" } },
            { "stream_64_uk_fhd", new Source { Id = "stream_64_uk_fhd", Name = "BBC STREAM 64 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_064/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_69_uk", new Source { Id = "stream_69_uk", Name = "BBC STREAM 69 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_069/" } },
            { "stream_69_uk_fhd", new Source { Id = "stream_69_uk_fhd", Name = "BBC STREAM 69 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_069/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_070_uk", new Source { Id = "stream_070_uk", Name = "BBC STREAM 70 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_070/" } },
            { "stream_070_uk_fhd", new Source { Id = "stream_070_uk_fhd", Name = "BBC STREAM 70 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_070/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_071_uk", new Source { Id = "stream_071_uk", Name = "BBC STREAM 71 [UK Only]", Category = "BBC Streams (UK)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_071/" } },
            { "stream_071_uk_fhd", new Source { Id = "stream_071_uk_fhd", Name = "BBC STREAM 71 [UK Only]", Category = "BBC Streams (UK) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:uk_bbc_stream_071/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },

            // streams (World)
            { "stream_01_ww", new Source { Id = "stream_01_ww", Name = "BBC STREAM 01", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_001/" } },
            { "stream_01_ww_fhd", new Source { Id = "stream_01_ww_fhd", Name = "BBC STREAM 01", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_001/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_02_ww", new Source { Id = "stream_02_ww", Name = "BBC STREAM 02", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_002/" } },
            { "stream_02_ww_fhd", new Source { Id = "stream_02_ww_fhd", Name = "BBC STREAM 02", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_002/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_51_ww", new Source { Id = "stream_51_ww", Name = "BBC STREAM 51", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_051/" } },
            { "stream_51_ww_fhd", new Source { Id = "stream_51_ww_fhd", Name = "BBC STREAM 51", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_051/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_52_ww", new Source { Id = "stream_52_ww", Name = "BBC STREAM 52", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_052/" } },
            { "stream_52_ww_fhd", new Source { Id = "stream_52_ww_fhd", Name = "BBC STREAM 52", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_052/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_53_ww", new Source { Id = "stream_53_ww", Name = "BBC STREAM 53", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_053/" } },
            { "stream_53_ww_fhd", new Source { Id = "stream_53_ww_fhd", Name = "BBC STREAM 53", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_053/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_54_ww", new Source { Id = "stream_54_ww", Name = "BBC STREAM 54", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_054/" } },
            { "stream_54_ww_fhd", new Source { Id = "stream_54_ww_fhd", Name = "BBC STREAM 54", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_054/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_55_ww", new Source { Id = "stream_55_ww", Name = "BBC STREAM 55", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_055/" } },
            { "stream_55_ww_fhd", new Source { Id = "stream_55_ww_fhd", Name = "BBC STREAM 55", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_055/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_56_ww", new Source { Id = "stream_56_ww", Name = "BBC STREAM 56", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_056/" } },
            { "stream_56_ww_fhd", new Source { Id = "stream_56_ww_fhd", Name = "BBC STREAM 56", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_056/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_57_ww", new Source { Id = "stream_57_ww", Name = "BBC STREAM 57", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_057/" } },
            { "stream_57_ww_fhd", new Source { Id = "stream_57_ww_fhd", Name = "BBC STREAM 57", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_057/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_58_ww", new Source { Id = "stream_58_ww", Name = "BBC STREAM 58", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_058/" } },
            { "stream_58_ww_fhd", new Source { Id = "stream_58_ww_fhd", Name = "BBC STREAM 58", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_058/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_59_ww", new Source { Id = "stream_59_ww", Name = "BBC STREAM 59", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_059/" } },
            { "stream_59_ww_fhd", new Source { Id = "stream_59_ww_fhd", Name = "BBC STREAM 59", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_059/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_60_ww", new Source { Id = "stream_60_ww", Name = "BBC STREAM 60", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_060/" } },
            { "stream_60_ww_fhd", new Source { Id = "stream_60_ww_fhd", Name = "BBC STREAM 60", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_060/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_61_ww", new Source { Id = "stream_61_ww", Name = "BBC STREAM 61", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_061/" } },
            { "stream_61_ww_fhd", new Source { Id = "stream_61_ww_fhd", Name = "BBC STREAM 61", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_061/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_62_ww", new Source { Id = "stream_62_ww", Name = "BBC STREAM 62", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_062/" } },
            { "stream_62_ww_fhd", new Source { Id = "stream_62_ww_fhd", Name = "BBC STREAM 62", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_062/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_63_ww", new Source { Id = "stream_63_ww", Name = "BBC STREAM 63", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_063/" } },
            { "stream_63_ww_fhd", new Source { Id = "stream_63_ww_fhd", Name = "BBC STREAM 63", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_063/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_64_ww", new Source { Id = "stream_64_ww", Name = "BBC STREAM 64", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_064/" } },
            { "stream_64_ww_fhd", new Source { Id = "stream_64_ww_fhd", Name = "BBC STREAM 64", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_064/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },
            { "stream_65_ww", new Source { Id = "stream_65_ww", Name = "BBC STREAM 65", Category = "BBC Streams (World)", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_065/" } },
            { "stream_65_ww_fhd", new Source { Id = "stream_65_ww_fhd", Name = "BBC STREAM 65", Category = "BBC Streams (World) FHD", UrlPrefix = "https://ve-cmaf-pushb-uk.live.fastly.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:ww_bbc_stream_065/", VideoPath = "v=pv66/b=6500000", AudioPath = "a=pa6/al=en-GB/ap=main/b=320000" } },

            // World service
            { "ws_05", new Source { Id = "ws_05", Name = "World Service Stream 05 (Urdu, Pashto, Burmese, Swahili, Arabic Services)", Category = "BBC World Service", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:world_service_stream_05/" } },
            { "ws_06", new Source { Id = "ws_06", Name = "World Service Stream 06 (Telugu, Tamil, Kyrgyz, Hindi, Ukranian Services)", Category = "BBC World Service", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:world_service_stream_06/" } },
            { "ws_07", new Source { Id = "ws_07", Name = "World Service Stream 07 (Afghan Retransmission)", Category = "BBC World Service", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:world_service_stream_07/" } },
            { "ws_08", new Source { Id = "ws_08", Name = "World Service Stream 08 (News Asia Pacific)", Category = "BBC World Service", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:world_service_stream_08/" } },
            { "ws_afghan", new Source { Id = "ws_afghan", Name = "BBC Afghanistan", Category = "BBC World Service", UrlPrefix = "https://vs-cmaf-pushb-ww.live.cf.md.bbci.co.uk/x=4/i=urn:bbc:pips:service:bbc_afghan_tv/" } }
        };

        public static Source GetSource(string id) => All.ContainsKey(id) ? All[id] : null;
    }




}
