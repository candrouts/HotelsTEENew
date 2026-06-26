using HotelsTEE.DAL;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using HotelsTEE.Models;

namespace HotelsTEE.AzureStorage
{
    public class AzureStorage
    {

        private static CloudStorageAccount CreateStorageAccountFromConnectionString(string storageConnectionString)
        {
            CloudStorageAccount storageAccount;
            try
            {
                storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            }
            catch (FormatException)
            {
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the sample.");
                Console.WriteLine("Press any key to exit");
                Console.ReadLine();
                throw;
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Invalid storage account information provided. Please confirm the AccountName and AccountKey are valid in the app.config file - then restart the sample.");
                Console.WriteLine("Press any key to exit");
                Console.ReadLine();
                throw;
            }

            return storageAccount;
        }



        public static async Task SaveFileToPaintings(List<HttpPostedFileBase> fileToUpload, string hotelCriteriaID,string criteriaFileID)
        {


            UnitOfWork unitOfWork = new UnitOfWork();

            string connectionString = WebConfigurationManager.AppSettings["StorageConnectionString"].ToString();

            CloudStorageAccount storageAccount = CreateStorageAccountFromConnectionString(connectionString);
            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            CloudFileShare share = fileClient.GetShareReference("paintings");
            CloudFileDirectory root = share.GetRootDirectoryReference();
            CloudFileDirectory dirRoot = root.GetDirectoryReference("greencert");

            CloudFileDirectory dir = dirRoot.GetDirectoryReference(hotelCriteriaID);
            if (!dir.Exists())
                dir.Create();

            CloudFileDirectory dir2 = dir.GetDirectoryReference(criteriaFileID);
            if (!dir2.Exists())
                dir2.Create();

            foreach (var z in fileToUpload)
            {
                string fileName = z.FileName.Replace("'", "_").Replace("&", "_").Replace(":", "_").Replace("+", "_").Replace("/", "_").Replace("#", "_");
                HotelCriteria_CriteriaFile newFile = new HotelCriteria_CriteriaFile();

                string originalFileName = "";
                string fileType = "";
                var splitted = fileName.Split('.');
                int cnt = 0;
                int length = splitted.Length - 1;
                foreach (var x in splitted)
                {
                    if (cnt == length)
                        fileType = x;
                    else
                        originalFileName += x;

                    cnt++;
                }

                string newFileName = originalFileName + "." + fileType;
                newFile.hotelCriteriaID = Convert.ToDecimal(hotelCriteriaID);
                newFile.criteriaFileID = Convert.ToDecimal(criteriaFileID);
                newFile.fileName = newFileName;
                newFile.fileType = fileType;

                newFile.creationDateTime = DateTime.Now;
              
                unitOfWork.HotelCriteria_CriteriaFileRepository.Insert(newFile);

                CloudFile file = dir2.GetFileReference(newFileName);
                file.UploadFromStream(z.InputStream);
            }

            try
            {
                unitOfWork.Save();

            }
            catch (Exception e)
            {

            }

        }



        // Αντιγραφή αρχείου τεκμηρίου από μια αξιολόγηση σε άλλη (π.χ. clone v2 → v3):
        // greencert/{source}/{criteriaFileID}/{fileName} → greencert/{target}/{criteriaFileID}/{fileName}
        public static void CopyFileBetweenCriteria(string sourceHotelCriteriaID, string targetHotelCriteriaID, string criteriaFileID, string fileName)
        {
            string connectionString = WebConfigurationManager.AppSettings["StorageConnectionString"].ToString();

            CloudStorageAccount storageAccount = CreateStorageAccountFromConnectionString(connectionString);
            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            CloudFileShare share = fileClient.GetShareReference("paintings");
            CloudFileDirectory root = share.GetRootDirectoryReference();
            CloudFileDirectory dirRoot = root.GetDirectoryReference("greencert");

            // Πηγή
            CloudFileDirectory srcDir = dirRoot.GetDirectoryReference(sourceHotelCriteriaID).GetDirectoryReference(criteriaFileID);
            if (!srcDir.Exists())
                return;

            CloudFile srcFile = srcDir.GetFileReference(fileName);
            if (!srcFile.Exists())
                return;

            // Προορισμός (δημιουργία φακέλων on-demand)
            CloudFileDirectory dstParent = dirRoot.GetDirectoryReference(targetHotelCriteriaID);
            if (!dstParent.Exists())
                dstParent.Create();

            CloudFileDirectory dstDir = dstParent.GetDirectoryReference(criteriaFileID);
            if (!dstDir.Exists())
                dstDir.Create();

            CloudFile dstFile = dstDir.GetFileReference(fileName);

            using (var stream = new System.IO.MemoryStream())
            {
                srcFile.DownloadToStream(stream);
                stream.Position = 0;
                dstFile.UploadFromStream(stream);
            }
        }


        // Κατέβασμα αρχείου τεκμηρίου: paintings/greencert/{hotelCriteriaID}/{criteriaFileID}/{fileName}
        public static System.IO.Stream GetFileFromFolder(string hotelCriteriaID, string criteriaFileID, string fileName)
        {
            string connectionString = WebConfigurationManager.AppSettings["StorageConnectionString"].ToString();

            CloudStorageAccount storageAccount = CreateStorageAccountFromConnectionString(connectionString);
            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            CloudFileShare share = fileClient.GetShareReference("paintings");
            CloudFileDirectory root = share.GetRootDirectoryReference();
            CloudFileDirectory dirRoot = root.GetDirectoryReference("greencert");

            CloudFileDirectory dir = dirRoot.GetDirectoryReference(hotelCriteriaID);
            CloudFileDirectory dir2 = dir.GetDirectoryReference(criteriaFileID);

            if (!dir2.Exists())
                return null;

            CloudFile file = dir2.GetFileReference(fileName);
            if (!file.Exists())
                return null;

            var stream = new System.IO.MemoryStream();
            file.DownloadToStream(stream);
            stream.Position = 0;
            return stream;
        }


        public static async Task RemoveFileFromFolder(string firstFolderName, string folderName, string fileName)
        {

            string connectionString = WebConfigurationManager.AppSettings["StorageConnectionString"].ToString();

            CloudStorageAccount storageAccount = CreateStorageAccountFromConnectionString(connectionString);
            CloudFileClient fileClient = storageAccount.CreateCloudFileClient();
            CloudFileShare share = fileClient.GetShareReference("paintings");
            CloudFileDirectory root = share.GetRootDirectoryReference();

            CloudFileDirectory dirRoot = root.GetDirectoryReference("greencert");


            CloudFileDirectory dir = dirRoot.GetDirectoryReference(firstFolderName);

            CloudFileDirectory dirToCopy = dir.GetDirectoryReference(folderName);

            if (dirToCopy.Exists())
            {

                CloudFile file = dirToCopy.GetFileReference(fileName);
                file.Delete();

            }

        }


    }
}