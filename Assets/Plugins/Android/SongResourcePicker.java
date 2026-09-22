package dev.openwds.resources;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.provider.DocumentsContract;
import android.content.ContentResolver;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

/** Opens only the system's local document picker. No networking or storage-wide permission. */
public final class SongResourcePicker extends Activity {
    private static final String RECEIVER = "OfflineSongResources";
    private static Uri selectedSource;
    private static int persistedFlags;
    private static void releaseSource() {
        if (selectedSource != null && persistedFlags != 0) {
            try { UnityPlayer.currentActivity.getContentResolver().releasePersistableUriPermission(selectedSource, persistedFlags); }
            catch (Exception ignored) { }
        }
        selectedSource = null; persistedFlags = 0;
    }
    public static void deleteImportedSource() {
        final Uri source = selectedSource;
        new Thread(() -> {
            boolean deleted = false;
            try {
                if (source != null) deleted = DocumentsContract.deleteDocument(UnityPlayer.currentActivity.getContentResolver(), source);
            } catch (Exception ignored) { }
            releaseSource();
            UnityPlayer.UnitySendMessage(RECEIVER, "OnSourceDeleted", deleted ? "deleted" : "retained");
        }, "OpenWDS source cleanup").start();
    }
    public static void abandonSource() { releaseSource(); }
    public static void open() {
        releaseSource();
        final Activity unity = UnityPlayer.currentActivity;
        unity.runOnUiThread(() -> unity.startActivity(new Intent(unity, SongResourcePicker.class)));
    }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (state == null) {
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
            intent.setType("application/zip");
            intent.putExtra(Intent.EXTRA_MIME_TYPES, new String[]{"application/zip", "application/x-zip-compressed", "application/octet-stream"});
            intent.putExtra(Intent.EXTRA_LOCAL_ONLY, true);
            try { startActivityForResult(intent, 1); }
            catch (Exception e) { fail(e); }
        }
    }
    private void fail(Exception e) {
        releaseSource();
        UnityPlayer.UnitySendMessage(RECEIVER, "OnPickError", e.toString());
        runOnUiThread(this::finish);
    }
    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != 1) return;
        if (result != RESULT_OK || data == null || data.getData() == null) {
            UnityPlayer.UnitySendMessage(RECEIVER, "OnPicked", "");
            finish();
            return;
        }
        final Uri uri = data.getData();
        selectedSource = uri;
        int flags = data.getFlags() & (Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
        try {
            getContentResolver().takePersistableUriPermission(uri, flags);
            persistedFlags = flags;
        } catch (Exception ignored) { /* Some local providers grant only temporary access. */ }
        final InputStream selected;
        try {
            selected = getContentResolver().openInputStream(uri);
            if (selected == null) throw new java.io.IOException("Cannot read selected file.");
        } catch (Exception e) { fail(e); return; }
        new Thread(() -> {
            File target = new File(getCacheDir(), "openwds-song-import.zip");
            try (InputStream input = selected;
                 FileOutputStream output = new FileOutputStream(target)) {
                if (input == null) throw new java.io.IOException("Cannot read selected file.");
                byte[] bytes = new byte[128 * 1024];
                int count;
                while ((count = input.read(bytes)) != -1) output.write(bytes, 0, count);
                output.getFD().sync();
            } catch (Exception e) {
                target.delete();
                fail(e);
                return;
            }
            UnityPlayer.UnitySendMessage(RECEIVER, "OnPicked", target.getAbsolutePath());
            runOnUiThread(this::finish);
        }, "OpenWDS offline import").start();
        // The open descriptor remains valid; show Unity's progress screen during the copy.
        finish();
    }
}
