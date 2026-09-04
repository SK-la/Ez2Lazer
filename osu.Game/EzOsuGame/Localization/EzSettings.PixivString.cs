// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Localization
{
    public class EzSettingsPixivString
    {
#region Pixiv 背景

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_AUTH_TOOL_HINT_PREFIX = new EzLocalizationManager.EzLocalisableString(
            "请从 ",
            "Download EzPixivAuth from ");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_AUTH_TOOL_HINT_LINK = new EzLocalizationManager.EzLocalisableString(
            "EzPixivAuth Releases",
            "EzPixivAuth Releases");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_AUTH_TOOL_HINT_SUFFIX = new EzLocalizationManager.EzLocalisableString(
            " 下载 EzPixivAuth，双击运行后会自动写入本机 pixiv_auth.json（与 client.realm 同目录）。",
            " and run it to write pixiv_auth.json next to client.realm.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CUSTOM_TOOL_HINT = new EzLocalizationManager.EzLocalisableString(
            "自定义",
            "Custom");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CUSTOM_TOOL_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "展开高级选项：手动 refresh_token、反代地址、过滤、黑白名单。",
            "Show advanced options: manual refresh_token, proxy URL, filters, and artist/tag lists.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_MANUAL_TOKEN_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "仅当你已从其他途径获得 refresh_token 时使用。输入为隐藏显示，勿泄露给他人。",
            "Only if you already have a refresh_token from elsewhere. Input is masked; do not share it.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_REFRESH_TOKEN = new EzLocalizationManager.EzLocalisableString(
            "refresh_token", "refresh_token");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_REFRESH_TOKEN_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "粘贴后点「保存」。仅保存在本机。",
            "Paste and click Save. Stored locally only.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_API_PROXY_BASE_URL = new EzLocalizationManager.EzLocalisableString(
            "反代地址", "Proxy base URL");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_API_PROXY_BASE_URL_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "替换 app-api.pixiv.net 与 i.pximg.net 请求（OAuth 登录仍直连）。"
            + "\n留空则全部直连官方。"
            + "\nCloudflare Workers 示例：https://pixiv.yourdomain.com"
            + "\nVercel 示例：https://your-project.vercel.app/api"
            + "\n参考：https://github.com/vmoranv/pixiv-proxy",
            "Rewrites app-api.pixiv.net API calls and i.pximg.net image downloads (OAuth login stays direct)."
            + "\nLeave empty to use official endpoints."
            + "\nCloudflare Workers example: https://pixiv.yourdomain.com"
            + "\nVercel example: https://your-project.vercel.app/api"
            + "\nSee: https://github.com/vmoranv/pixiv-proxy");

        public const string PIXIV_AUTH_TOOL_RELEASES_URL = "https://github.com/SK-la/EzPixivAuth/releases";

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_SAVE_TOKEN = new EzLocalizationManager.EzLocalisableString("保存", "Save");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_SAVE_TOKEN_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "将上方输入框中的 refresh_token 保存到本机。",
            "Saves the refresh_token from the input field above to local storage.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CHECK_LOGIN = new EzLocalizationManager.EzLocalisableString("检查登录", "Check login");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CHECK_LOGIN_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "验证凭证是否可用。", "Verifies the saved credential.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_STATUS_NOT_CONFIGURED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv：尚未保存登录凭证。", "Pixiv: no credential saved yet.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_STATUS_LOGGED_IN = new EzLocalizationManager.EzLocalisableString(
            "Pixiv：已登录 @{0}", "Pixiv: logged in as @{0}");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_STATUS_INVALID = new EzLocalizationManager.EzLocalisableString(
            "Pixiv：凭证无效，请重新运行 EzPixivAuth 或手动保存。", "Pixiv: invalid credential; run EzPixivAuth again or save manually.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CLEAR_TOKEN = new EzLocalizationManager.EzLocalisableString("清除", "Clear");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_CLEAR_TOKEN_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "删除本机保存的凭证。", "Deletes the locally saved credential.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TOKEN_SAVED = new EzLocalizationManager.EzLocalisableString("已保存。", "Saved.");
        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TOKEN_CLEARED = new EzLocalizationManager.EzLocalisableString("已清除。", "Cleared.");
        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TOKEN_EMPTY = new EzLocalizationManager.EzLocalisableString("请先粘贴 refresh_token。", "Paste a refresh_token first.");
        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_VERIFY_SUCCESS = new EzLocalizationManager.EzLocalisableString("Pixiv 登录成功：@{0}", "Pixiv login OK: @{0}");
        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_VERIFY_FAILED = new EzLocalizationManager.EzLocalisableString("Pixiv 登录验证失败。", "Pixiv login verification failed.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_TOKEN_REFRESH_FAILED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 凭证刷新失败。", "Pixiv token refresh failed.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_TOKEN_REFRESH_EMPTY = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 凭证刷新未返回 access token。", "Pixiv token refresh returned an empty access token.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_ACCESS_TOKEN_NOT_SET = new EzLocalizationManager.EzLocalisableString(
            "Pixiv access token 未就绪。", "Pixiv access token is not set.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_FOLLOW_FEED_FAILED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 关注流加载失败。", "Failed to load Pixiv follow feed.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_FOLLOW_FEED_EMPTY = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 关注流过滤后无可用插图。", "Pixiv follow feed returned no illustrations after filtering.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_IMAGE_DOWNLOAD_FAILED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 插图下载失败。", "Failed to download Pixiv image.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_IMAGE_EMPTY = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 插图下载结果为空。", "Downloaded Pixiv image was empty.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ERROR_REQUEST_FAILED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 网络请求失败。", "Pixiv network request failed.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_LOG_SONG_CHANGE_DOWNLOAD = new EzLocalizationManager.EzLocalisableString(
            "切歌下载", "Song change download");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_LOG_AUTO_PREFETCH = new EzLocalizationManager.EzLocalisableString(
            "自动预缓存", "Auto prefetch");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_AUTO_DOWNLOAD_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "Pixiv 自动下载", "Pixiv background auto-download");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_AUTO_DOWNLOAD_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "切歌时优先显示 BG_PIXIV 已有图片（不阻塞界面），并在后台每次追加下载 1 张。"
            + "\n开启后额外持续预缓存；未缓存不足 10 张时自动再拉一页关注流。",
            "Song changes show a random cached BG_PIXIV image immediately without blocking, while one new illustration downloads in the background."
            + "\nWhen enabled, also keeps prefetching and fetches another feed page when fewer than 10 are cached.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ALLOW_R18 = new EzLocalizationManager.EzLocalisableString(
            "允许 R-18 作品", "Allow R-18 works");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ALLOW_R18_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "关闭时过滤 sanity_level ≥ 4 或含 R-18 标签的作品。",
            "When disabled, filters works with sanity_level ≥ 4 or R-18 tags.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_LANDSCAPE_ONLY = new EzLocalizationManager.EzLocalisableString(
            "仅横图", "Landscape only");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_LANDSCAPE_ONLY_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "开启时只接受宽大于高的第一页（p0；多页作品不会选用 p1 及之后）。",
            "When enabled, only accepts page 0 (p0) when wider than tall; later pages of multi-page works are never used.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ACCOUNT_WHITELIST = new EzLocalizationManager.EzLocalisableString(
            "画师 account 白名单", "Artist account whitelist");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ACCOUNT_WHITELIST_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "非空时仅允许列表中的画师 account 句柄；多项用空格分隔（与 osu 标签类似，也可用逗号/分号）。",
            "When non-empty, only listed artist account handles are allowed. Separate with spaces (like osu tags), or commas/semicolons.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ACCOUNT_BLACKLIST = new EzLocalizationManager.EzLocalisableString(
            "画师 account 黑名单", "Artist account blacklist");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_ACCOUNT_BLACKLIST_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "命中则跳过；多项用空格分隔（也可用逗号/分号）。",
            "Matching accounts are skipped. Separate with spaces, or commas/semicolons.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TAG_INCLUDE = new EzLocalizationManager.EzLocalisableString(
            "标签包含（任一）", "Tag include (any)");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TAG_INCLUDE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "非空时作品须至少包含其中一个标签（不区分大小写）；多项用空格分隔。含空格的标签请用逗号括起，如：女の子,AI Generated",
            "When non-empty, works must include at least one listed tag (case-insensitive). Separate with spaces; use commas for tags that contain spaces.");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TAG_EXCLUDE = new EzLocalizationManager.EzLocalisableString(
            "标签排除", "Tag exclude");

        public static readonly EzLocalizationManager.EzLocalisableString PIXIV_TAG_EXCLUDE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "命中任一标签则跳过；多项用空格分隔。含空格的标签请用逗号括起。",
            "Works with any listed tag are skipped. Separate with spaces; use commas for tags that contain spaces.");

        #endregion
    }
}
